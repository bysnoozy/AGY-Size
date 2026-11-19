using System.Buffers.Binary;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace AgySize.Core.Scanning.Mft;

/// <summary>
/// Index en mémoire de la table MFT (Master File Table) d'un volume NTFS, construit par lecture
/// directe et séquentielle des enregistrements de la MFT — sans passer par l'énumération de
/// dossiers de Windows, ce qui est considérablement plus rapide sur de gros volumes (quelques
/// lectures disque contiguës au lieu d'une requête par dossier).
///
/// Pour chaque enregistrement (identifié par son numéro, "FRN"), on conserve uniquement ce dont
/// le scan a besoin : dossier parent, nom (espace de noms Win32/POSIX de préférence au nom court
/// DOS 8.3), taille du flux de données principal, date de dernière modification
/// ($STANDARD_INFORMATION) et le fait qu'il s'agit d'un dossier.
///
/// Un fichier à plusieurs liens physiques apparaît sous chacun de ses noms (comme lors d'une
/// énumération classique) ; les flux de données alternatifs (ADS) ne sont pas comptés, comme pour
/// <see cref="FileInfo.Length"/>.
/// </summary>
public sealed class MftIndex
{
    /// <summary>Numéro d'enregistrement MFT du dossier racine du volume.</summary>
    public const int RootRecord = 5;

    /// <summary>Les 16 premiers enregistrements sont les métafichiers NTFS ($MFT, $LogFile, $Extend...).</summary>
    private const int FirstUserRecord = 16;

    private const uint AttrStandardInformation = 0x10;
    private const uint AttrAttributeList = 0x20;
    private const uint AttrFileName = 0x30;
    private const uint AttrData = 0x80;
    private const uint AttrEnd = 0xFFFFFFFF;

    private const byte NamespaceDos = 2;
    private const byte NoName = 0xFF;

    private const int ReadChunkBytes = 4 * 1024 * 1024;

    private readonly int[] _parent;
    private readonly string?[] _names;
    private readonly byte[] _nameNamespace;
    private readonly long[] _sizes;
    private readonly long[] _lastWriteFileTime;
    private readonly bool[] _isDirectory;
    private readonly bool[] _inUse;
    private readonly List<(int Record, int Parent, string Name)> _extraLinks = new();
    private int[] _firstChild = Array.Empty<int>();
    private int[] _nextSibling = Array.Empty<int>();

    private MftIndex(int recordCount)
    {
        RecordCount = recordCount;
        _parent = new int[recordCount];
        _names = new string?[recordCount];
        _nameNamespace = new byte[recordCount];
        Array.Fill(_nameNamespace, NoName);
        _sizes = new long[recordCount];
        _lastWriteFileTime = new long[recordCount];
        _isDirectory = new bool[recordCount];
        _inUse = new bool[recordCount];
    }

    public int RecordCount { get; }

    /// <summary>Nombre d'enregistrements en usage portant un nom (fichiers + dossiers du volume).</summary>
    public int EntryCount { get; private set; }

    // Les identifiants renvoyés par GetChildren sont des numéros d'enregistrement, sauf pour les
    // liens physiques supplémentaires : RecordCount + position dans _extraLinks.
    private int RecordOf(int id) => id < RecordCount ? id : _extraLinks[id - RecordCount].Record;

    public string? GetName(int id) => id < RecordCount ? _names[id] : _extraLinks[id - RecordCount].Name;

    public bool IsDirectory(int id) => _isDirectory[RecordOf(id)];

    public long GetSize(int id) => _sizes[RecordOf(id)];

    public DateTime GetLastWriteUtc(int id)
    {
        var fileTime = _lastWriteFileTime[RecordOf(id)];
        return fileTime is > 0 and < 2650467743999999999 // DateTime.MaxValue en FILETIME
            ? DateTime.FromFileTimeUtc(fileTime)
            : DateTime.MinValue;
    }

    /// <summary>Enfants directs d'un dossier (identifiants d'entrée), métafichiers NTFS exclus.</summary>
    public IEnumerable<int> GetChildren(int directory)
    {
        directory = RecordOf(directory);
        for (var child = _firstChild[directory]; child != 0; child = _nextSibling[child])
        {
            yield return child;
        }
    }

    /// <summary>
    /// Retrouve un dossier à partir de son chemin relatif à la racine du volume (segments séparés
    /// par '\' ou '/', comparaison insensible à la casse comme sous Windows). Renvoie null s'il
    /// n'existe pas ou n'est pas un dossier.
    /// </summary>
    public int? FindDirectory(string relativePath)
    {
        var current = RootRecord;
        foreach (var segment in relativePath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int? next = null;
            foreach (var child in GetChildren(current))
            {
                if (_isDirectory[child] && string.Equals(_names[child], segment, StringComparison.OrdinalIgnoreCase))
                {
                    next = child;
                    break;
                }
            }

            if (next is null)
            {
                return null;
            }

            current = next.Value;
        }

        return current;
    }

    /// <summary>Ouvre un volume (ex. <c>\\.\D:</c>, droits administrateur requis) ou une image disque et lit sa MFT.</summary>
    public static MftIndex ReadVolume(string devicePath, IProgress<long>? recordsRead = null, CancellationToken cancellationToken = default)
    {
        using var handle = File.OpenHandle(devicePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Read(handle, recordsRead, cancellationToken);
    }

    public static MftIndex Read(SafeFileHandle handle, IProgress<long>? recordsRead = null, CancellationToken cancellationToken = default)
    {
        // Lecture du secteur d'amorçage (4 Ko : couvre aussi les disques à secteurs natifs de 4 Ko,
        // pour lesquels une lecture brute doit être alignée sur la taille de secteur).
        var boot = new byte[4096];
        ReadExactly(handle, boot, 0);

        if (Encoding.ASCII.GetString(boot, 3, 8) != "NTFS    ")
        {
            throw new InvalidDataException("Le volume n'est pas un volume NTFS.");
        }

        var bytesPerSector = BinaryPrimitives.ReadUInt16LittleEndian(boot.AsSpan(0x0B));
        var sectorsPerClusterRaw = boot[0x0D];
        var sectorsPerCluster = sectorsPerClusterRaw <= 0x80 ? sectorsPerClusterRaw : 1 << (256 - sectorsPerClusterRaw);
        long clusterSize = (long)bytesPerSector * sectorsPerCluster;
        var mftCluster = BinaryPrimitives.ReadInt64LittleEndian(boot.AsSpan(0x30));
        var clustersPerRecord = (sbyte)boot[0x40];
        var recordSize = clustersPerRecord > 0 ? (int)(clustersPerRecord * clusterSize) : 1 << -clustersPerRecord;

        if (bytesPerSector == 0 || clusterSize == 0 || recordSize is < 512 or > 65536 || clusterSize % 512 != 0)
        {
            throw new InvalidDataException("Secteur d'amorçage NTFS incohérent.");
        }

        // Enregistrement 0 = $MFT lui-même : son flux $DATA décrit où se trouve toute la MFT sur le disque.
        var mftRecordOffset = mftCluster * clusterSize;
        var record0 = new byte[Math.Max(recordSize, (int)Math.Min(clusterSize, 65536))];
        ReadExactly(handle, record0, mftRecordOffset);
        var record0Span = record0.AsSpan(0, recordSize);
        if (!ApplyFixups(record0Span))
        {
            throw new InvalidDataException("Enregistrement $MFT illisible.");
        }

        var (mftSize, extents) = ReadMftDataExtents(handle, record0Span, clusterSize, recordSize, mftRecordOffset);

        var recordCount = (int)Math.Min(int.MaxValue, mftSize / recordSize);
        var index = new MftIndex(recordCount);

        var chunkBytes = (int)(ReadChunkBytes / clusterSize * clusterSize);
        chunkBytes = Math.Max(chunkBytes, (int)clusterSize);
        chunkBytes = chunkBytes / recordSize * recordSize;
        if (chunkBytes == 0)
        {
            chunkBytes = (int)Math.Max(recordSize, clusterSize);
        }

        var buffer = new byte[chunkBytes];
        long recordNumber = 0;
        long lastReported = 0;

        foreach (var (lcn, clusterCount) in extents)
        {
            var extentOffset = lcn * clusterSize;
            var extentBytes = clusterCount * clusterSize;
            long done = 0;
            while (done < extentBytes && recordNumber < recordCount)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var toRead = (int)Math.Min(chunkBytes, extentBytes - done);
                ReadExactly(handle, buffer.AsSpan(0, toRead), extentOffset + done);

                for (var pos = 0; pos + recordSize <= toRead && recordNumber < recordCount; pos += recordSize)
                {
                    index.ParseRecord((int)recordNumber, buffer.AsSpan(pos, recordSize));
                    recordNumber++;
                }

                done += toRead;

                if (recordNumber - lastReported >= 100_000)
                {
                    lastReported = recordNumber;
                    recordsRead?.Report(recordNumber);
                }
            }
        }

        index.BuildChildLinks();
        recordsRead?.Report(recordNumber);
        return index;
    }

    private void ParseRecord(int recordNumber, Span<byte> record)
    {
        if (!ApplyFixups(record))
        {
            return;
        }

        var flags = BinaryPrimitives.ReadUInt16LittleEndian(record[0x16..]);
        if ((flags & 0x01) == 0)
        {
            return; // enregistrement libre (fichier supprimé)
        }

        // Un enregistrement d'extension porte des attributs (noms, flux) qui débordent de
        // l'enregistrement de base d'un fichier : on les rattache à celui-ci.
        var baseRecord = (long)(BinaryPrimitives.ReadUInt64LittleEndian(record[0x20..]) & 0x0000FFFFFFFFFFFF);
        var target = recordNumber;
        if (baseRecord != 0)
        {
            if (baseRecord >= RecordCount)
            {
                return;
            }

            target = (int)baseRecord;
        }
        else
        {
            _inUse[target] = true;
            _isDirectory[target] = (flags & 0x02) != 0;
        }

        int offset = BinaryPrimitives.ReadUInt16LittleEndian(record[0x14..]);
        while (offset + 16 <= record.Length)
        {
            var type = BinaryPrimitives.ReadUInt32LittleEndian(record[offset..]);
            if (type == AttrEnd)
            {
                break;
            }

            var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(record[(offset + 4)..]);
            if (length < 16 || offset + length > record.Length)
            {
                break;
            }

            var attribute = record.Slice(offset, length);
            var nonResident = attribute[8] != 0;
            var nameLength = attribute[9];

            switch (type)
            {
                case AttrStandardInformation when !nonResident:
                {
                    var content = ResidentContent(attribute);
                    if (content.Length >= 16)
                    {
                        _lastWriteFileTime[target] = BinaryPrimitives.ReadInt64LittleEndian(content[8..]);
                    }

                    break;
                }

                case AttrFileName when !nonResident:
                {
                    var content = ResidentContent(attribute);
                    if (content.Length < 0x42)
                    {
                        break;
                    }

                    var charCount = content[0x40];
                    var nameSpace = content[0x41];
                    if (content.Length < 0x42 + charCount * 2)
                    {
                        break;
                    }

                    // On garde le premier nom long rencontré ; le nom court DOS 8.3 ne sert que
                    // s'il est le seul disponible.
                    var parentRef = (long)(BinaryPrimitives.ReadUInt64LittleEndian(content) & 0x0000FFFFFFFFFFFF);
                    var parent = parentRef < RecordCount ? (int)parentRef : -1;
                    var current = _nameNamespace[target];
                    var better = current == NoName || (current == NamespaceDos && nameSpace != NamespaceDos);
                    if (better)
                    {
                        _parent[target] = parent;
                        _names[target] = Encoding.Unicode.GetString(content.Slice(0x42, charCount * 2));
                        _nameNamespace[target] = nameSpace;
                    }
                    else if (nameSpace != NamespaceDos && current != NamespaceDos)
                    {
                        // Lien physique supplémentaire (autre nom long, éventuellement dans un autre dossier).
                        var name = Encoding.Unicode.GetString(content.Slice(0x42, charCount * 2));
                        if (parent != _parent[target] || name != _names[target])
                        {
                            _extraLinks.Add((target, parent, name));
                        }
                    }

                    break;
                }

                case AttrData when nameLength == 0:
                {
                    if (!nonResident)
                    {
                        _sizes[target] = ResidentContent(attribute).Length;
                    }
                    else if (attribute.Length >= 0x38 && BinaryPrimitives.ReadInt64LittleEndian(attribute[0x10..]) == 0)
                    {
                        // Seul le premier segment (VCN de départ 0) porte la taille réelle du flux.
                        _sizes[target] = BinaryPrimitives.ReadInt64LittleEndian(attribute[0x30..]);
                    }

                    break;
                }
            }

            offset += length;
        }
    }

    private void BuildChildLinks()
    {
        _firstChild = new int[RecordCount];
        _nextSibling = new int[RecordCount + _extraLinks.Count];

        for (var i = _extraLinks.Count - 1; i >= 0; i--)
        {
            var (record, parent, _) = _extraLinks[i];
            if (record < FirstUserRecord || !_inUse[record] || parent < 0 || !_inUse[parent] || !_isDirectory[parent]
                || (parent < FirstUserRecord && parent != RootRecord))
            {
                continue;
            }

            var id = RecordCount + i;
            _nextSibling[id] = _firstChild[parent];
            _firstChild[parent] = id;
            EntryCount++;
        }

        // Parcours à rebours pour que les listes chaînées d'enfants restent dans l'ordre de la MFT.
        for (var record = RecordCount - 1; record >= FirstUserRecord; record--)
        {
            if (!_inUse[record] || _names[record] is null)
            {
                continue;
            }

            var parent = _parent[record];
            if (parent < 0 || parent == record || !_inUse[parent] || !_isDirectory[parent])
            {
                continue;
            }

            // Les enfants d'un métafichier (ex. $Extend\$RmMetadata) restent invisibles, comme dans
            // l'Explorateur Windows.
            if (parent < FirstUserRecord && parent != RootRecord)
            {
                continue;
            }

            _nextSibling[record] = _firstChild[parent];
            _firstChild[parent] = record;
            EntryCount++;
        }
    }

    private static (long Size, List<(long Lcn, long Clusters)> Extents) ReadMftDataExtents(
        SafeFileHandle handle, Span<byte> record0, long clusterSize, int recordSize, long mftOffset)
    {
        var segments = new List<(long StartVcn, List<(long Lcn, long Clusters)> Runs)>();
        long size = -1;
        byte[]? attributeList = null;

        foreach (var attribute in EnumerateAttributes(record0))
        {
            var type = BinaryPrimitives.ReadUInt32LittleEndian(attribute);
            if (type == AttrData && attribute[9] == 0 && attribute[8] != 0)
            {
                var startVcn = BinaryPrimitives.ReadInt64LittleEndian(attribute[0x10..]);
                if (startVcn == 0)
                {
                    size = BinaryPrimitives.ReadInt64LittleEndian(attribute[0x30..]);
                }

                segments.Add((startVcn, ParseRunList(attribute)));
            }
            else if (type == AttrAttributeList)
            {
                attributeList = attribute[8] == 0
                    ? ResidentContent(attribute).ToArray()
                    : ReadNonResident(handle, attribute, clusterSize);
            }
        }

        // MFT très fragmentée : la liste de ses fragments déborde dans des enregistrements
        // d'extension, référencés par la liste d'attributs ($ATTRIBUTE_LIST) de l'enregistrement 0.
        if (attributeList is not null)
        {
            var knownRuns = segments.SelectMany(s => s.Runs).ToList();
            var position = 0;
            while (position + 0x1A <= attributeList.Length)
            {
                var entry = attributeList.AsSpan(position);
                var type = BinaryPrimitives.ReadUInt32LittleEndian(entry);
                int length = BinaryPrimitives.ReadUInt16LittleEndian(entry[4..]);
                if (length == 0)
                {
                    break;
                }

                var nameLength = entry[6];
                var reference = (long)(BinaryPrimitives.ReadUInt64LittleEndian(entry[0x10..]) & 0x0000FFFFFFFFFFFF);
                if (type == AttrData && nameLength == 0 && reference != 0)
                {
                    var extension = new byte[recordSize];
                    ReadExactlyRecord(handle, extension, reference, recordSize, clusterSize, knownRuns, mftOffset);
                    if (ApplyFixups(extension))
                    {
                        foreach (var attribute in EnumerateAttributes(extension))
                        {
                            if (BinaryPrimitives.ReadUInt32LittleEndian(attribute) == AttrData && attribute[9] == 0 && attribute[8] != 0)
                            {
                                var startVcn = BinaryPrimitives.ReadInt64LittleEndian(attribute[0x10..]);
                                if (segments.All(s => s.StartVcn != startVcn))
                                {
                                    var runs = ParseRunList(attribute);
                                    segments.Add((startVcn, runs));
                                    knownRuns.AddRange(runs);
                                }
                            }
                        }
                    }
                }

                position += length;
            }
        }

        if (size <= 0 || segments.Count == 0)
        {
            throw new InvalidDataException("Flux $DATA de la MFT introuvable.");
        }

        var extents = segments.OrderBy(s => s.StartVcn).SelectMany(s => s.Runs).ToList();
        return (size, extents);
    }

    /// <summary>Lit un enregistrement MFT par son numéro, à partir des fragments de MFT déjà connus.</summary>
    private static void ReadExactlyRecord(
        SafeFileHandle handle, byte[] destination, long recordNumber, int recordSize, long clusterSize,
        List<(long Lcn, long Clusters)> runs, long mftOffset)
    {
        var byteOffset = recordNumber * recordSize;
        foreach (var (lcn, clusters) in runs)
        {
            var runBytes = clusters * clusterSize;
            if (byteOffset < runBytes)
            {
                // Lecture alignée sur les clusters contenant l'enregistrement (exigé pour un volume brut).
                var absolute = lcn * clusterSize + byteOffset;
                var aligned = absolute / clusterSize * clusterSize;
                var end = (absolute + recordSize + clusterSize - 1) / clusterSize * clusterSize;
                var span = new byte[end - aligned];
                ReadExactly(handle, span, aligned);
                Array.Copy(span, absolute - aligned, destination, 0, recordSize);
                return;
            }

            byteOffset -= runBytes;
        }

        throw new InvalidDataException($"Enregistrement MFT {recordNumber} hors des fragments connus (MFT à {mftOffset}).");
    }

    private static byte[] ReadNonResident(SafeFileHandle handle, ReadOnlySpan<byte> attribute, long clusterSize)
    {
        var realSize = BinaryPrimitives.ReadInt64LittleEndian(attribute[0x30..]);
        var result = new byte[realSize];
        long written = 0;
        foreach (var (lcn, clusters) in ParseRunList(attribute))
        {
            var bytes = new byte[clusters * clusterSize];
            ReadExactly(handle, bytes, lcn * clusterSize);
            var count = (int)Math.Min(bytes.Length, realSize - written);
            Array.Copy(bytes, 0, result, written, count);
            written += count;
            if (written >= realSize)
            {
                break;
            }
        }

        return result;
    }

    private static IEnumerable<byte[]> EnumerateAttributes(Span<byte> record) => EnumerateAttributes(record.ToArray());

    private static IEnumerable<byte[]> EnumerateAttributes(byte[] record)
    {
        int offset = BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(0x14));
        while (offset + 16 <= record.Length)
        {
            var type = BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(offset));
            if (type == AttrEnd)
            {
                yield break;
            }

            var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(offset + 4));
            if (length < 16 || offset + length > record.Length)
            {
                yield break;
            }

            yield return record.AsSpan(offset, length).ToArray();
            offset += length;
        }
    }

    /// <summary>Décode la liste de fragments ("runlist") d'un attribut non résident en (LCN, nombre de clusters).</summary>
    internal static List<(long Lcn, long Clusters)> ParseRunList(ReadOnlySpan<byte> attribute)
    {
        var runs = new List<(long, long)>();
        int position = BinaryPrimitives.ReadUInt16LittleEndian(attribute[0x20..]);
        long lcn = 0;
        while (position < attribute.Length && attribute[position] != 0)
        {
            var header = attribute[position++];
            var lengthSize = header & 0x0F;
            var offsetSize = header >> 4;
            if (lengthSize == 0 || lengthSize > 8 || offsetSize > 8 || position + lengthSize + offsetSize > attribute.Length)
            {
                break;
            }

            long clusters = 0;
            for (var i = 0; i < lengthSize; i++)
            {
                clusters |= (long)attribute[position + i] << (8 * i);
            }

            position += lengthSize;

            if (offsetSize == 0)
            {
                continue; // fragment creux (sparse) : rien à lire sur le disque
            }

            long delta = 0;
            for (var i = 0; i < offsetSize; i++)
            {
                delta |= (long)attribute[position + i] << (8 * i);
            }

            // Extension de signe : le décalage est relatif au fragment précédent et peut être négatif.
            if ((attribute[position + offsetSize - 1] & 0x80) != 0 && offsetSize < 8)
            {
                delta |= -1L << (8 * offsetSize);
            }

            position += offsetSize;
            lcn += delta;
            runs.Add((lcn, clusters));
        }

        return runs;
    }

    /// <summary>
    /// Applique les corrections de fin de secteur ("update sequence array") d'un enregistrement
    /// NTFS : les 2 derniers octets de chaque secteur ont été remplacés sur disque par un numéro de
    /// contrôle, et leur vraie valeur est rangée dans le tableau en tête d'enregistrement.
    /// Renvoie false si l'enregistrement n'est pas valide (signature absente ou secteur incohérent).
    /// </summary>
    internal static bool ApplyFixups(Span<byte> record)
    {
        if (record.Length < 0x30 || record[0] != (byte)'F' || record[1] != (byte)'I' || record[2] != (byte)'L' || record[3] != (byte)'E')
        {
            return false;
        }

        int usaOffset = BinaryPrimitives.ReadUInt16LittleEndian(record[0x04..]);
        int usaCount = BinaryPrimitives.ReadUInt16LittleEndian(record[0x06..]);
        if (usaCount < 2 || usaOffset + usaCount * 2 > record.Length)
        {
            return false;
        }

        var stride = record.Length / (usaCount - 1);
        if (stride < 2 || stride * (usaCount - 1) != record.Length)
        {
            return false;
        }

        var check = BinaryPrimitives.ReadUInt16LittleEndian(record[usaOffset..]);
        for (var i = 1; i < usaCount; i++)
        {
            var position = i * stride - 2;
            if (BinaryPrimitives.ReadUInt16LittleEndian(record[position..]) != check)
            {
                return false;
            }

            record[position] = record[usaOffset + i * 2];
            record[position + 1] = record[usaOffset + i * 2 + 1];
        }

        return true;
    }

    private static ReadOnlySpan<byte> ResidentContent(ReadOnlySpan<byte> attribute)
    {
        var contentLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(attribute[0x10..]);
        var contentOffset = BinaryPrimitives.ReadUInt16LittleEndian(attribute[0x14..]);
        return contentOffset + (long)contentLength <= attribute.Length
            ? attribute.Slice(contentOffset, contentLength)
            : ReadOnlySpan<byte>.Empty;
    }

    private static void ReadExactly(SafeFileHandle handle, Span<byte> buffer, long offset)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = RandomAccess.Read(handle, buffer[total..], offset + total);
            if (read <= 0)
            {
                throw new EndOfStreamException($"Lecture du volume interrompue à l'octet {offset + total}.");
            }

            total += read;
        }
    }
}
