using AgySize.Core.Scanning.Mft;

namespace AgySize.Core.Scanning;

/// <summary>Élément (fichier ou dossier) tel que fourni au scanner par une source d'énumération.</summary>
public readonly record struct ScanEntry(
    string Name,
    string FullPath,
    bool IsDirectory,
    long Size,
    DateTime LastWriteUtc,
    int Id = 0,
    string? Error = null);

/// <summary>
/// Source d'énumération d'une arborescence : soit l'énumération classique de dossiers
/// (<see cref="DirectoryScanSource"/>), soit la lecture directe de la MFT NTFS
/// (<see cref="MftScanSource"/>), beaucoup plus rapide sur un volume local.
/// </summary>
public interface IScanSource
{
    /// <summary>Libellé de la méthode d'énumération, pour le journal ("énumération classique", "lecture directe de la MFT"...).</summary>
    string Description { get; }

    ScanEntry Root { get; }

    /// <summary>Enfants directs d'un dossier. Peut lever <see cref="UnauthorizedAccessException"/> ou <see cref="IOException"/>.</summary>
    IEnumerable<ScanEntry> GetChildren(ScanEntry directory);
}

public sealed class DirectoryScanSource : IScanSource
{
    public DirectoryScanSource(string rootPath)
    {
        var info = new DirectoryInfo(rootPath);
        if (!info.Exists)
        {
            throw new DirectoryNotFoundException($"Dossier introuvable : {rootPath}");
        }

        Root = new ScanEntry(info.Name, info.FullName, true, 0, info.LastWriteTimeUtc);
    }

    public string Description => "énumération classique des dossiers";

    public ScanEntry Root { get; }

    public IEnumerable<ScanEntry> GetChildren(ScanEntry directory)
    {
        var children = new DirectoryInfo(directory.FullPath).GetFileSystemInfos();
        var entries = new List<ScanEntry>(children.Length);
        foreach (var child in children)
        {
            if (child is DirectoryInfo subDirectory)
            {
                entries.Add(new ScanEntry(child.Name, subDirectory.FullName, true, 0, subDirectory.LastWriteTimeUtc));
            }
            else if (child is FileInfo file)
            {
                try
                {
                    entries.Add(new ScanEntry(child.Name, file.FullName, false, file.Length, file.LastWriteTimeUtc));
                }
                catch (IOException ex)
                {
                    entries.Add(new ScanEntry(child.Name, file.FullName, false, 0, default, Error: ex.Message));
                }
            }
        }

        return entries;
    }
}

/// <summary>Énumère un dossier d'un volume NTFS à partir d'un <see cref="MftIndex"/> déjà lu.</summary>
public sealed class MftScanSource : IScanSource
{
    private readonly MftIndex _index;

    /// <param name="index">MFT du volume.</param>
    /// <param name="rootFullPath">Chemin complet du dossier à analyser (ex. <c>D:\Partage</c>), utilisé pour construire les chemins.</param>
    /// <param name="pathInVolume">Chemin de ce dossier relatif à la racine du volume (ex. <c>Partage</c> ; vide pour la racine).</param>
    public MftScanSource(MftIndex index, string rootFullPath, string pathInVolume)
    {
        _index = index;
        var record = index.FindDirectory(pathInVolume)
            ?? throw new DirectoryNotFoundException($"Dossier introuvable dans la MFT : {rootFullPath}");

        var name = pathInVolume.Length == 0 ? rootFullPath : Path.GetFileName(rootFullPath.TrimEnd('\\', '/'));
        Root = new ScanEntry(name, rootFullPath, true, 0, index.GetLastWriteUtc(record), record);
    }

    public string Description => "lecture directe de la MFT NTFS";

    public ScanEntry Root { get; }

    public IEnumerable<ScanEntry> GetChildren(ScanEntry directory)
    {
        foreach (var child in _index.GetChildren(directory.Id))
        {
            var name = _index.GetName(child)!;
            var isDirectory = _index.IsDirectory(child);
            yield return new ScanEntry(
                name,
                Path.Join(directory.FullPath, name),
                isDirectory,
                isDirectory ? 0 : _index.GetSize(child),
                _index.GetLastWriteUtc(child),
                child);
        }
    }

    /// <summary>
    /// Tente de préparer une source MFT pour <paramref name="rootPath"/> : uniquement sous Windows,
    /// pour un dossier situé sur un volume NTFS local (lettre de lecteur, pas de partage réseau),
    /// avec les droits administrateur (lecture brute du volume). Renvoie null avec la raison sinon,
    /// pour que l'appelant se replie sur l'énumération classique.
    /// </summary>
    public static MftScanSource? TryCreate(string rootPath, IProgress<long>? recordsRead, CancellationToken cancellationToken, out string? reason)
    {
        reason = null;
        if (!OperatingSystem.IsWindows())
        {
            reason = "lecture MFT disponible uniquement sous Windows";
            return null;
        }

        var fullPath = Path.GetFullPath(rootPath);
        var volumeRoot = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(volumeRoot) || volumeRoot.StartsWith(@"\\", StringComparison.Ordinal) || volumeRoot.Length < 2 || volumeRoot[1] != ':')
        {
            reason = "chemin réseau (UNC) : la MFT n'est lisible que sur un volume local — lancez AGY-Size directement sur le serveur";
            return null;
        }

        try
        {
            var drive = new DriveInfo(volumeRoot);
            if (drive.DriveType is DriveType.Network)
            {
                reason = "lecteur réseau : la MFT n'est lisible que sur un volume local — lancez AGY-Size directement sur le serveur";
                return null;
            }

            if (!string.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
            {
                reason = $"volume {drive.DriveFormat}, pas NTFS";
                return null;
            }

            var index = MftIndex.ReadVolume($@"\\.\{volumeRoot[0]}:", recordsRead, cancellationToken);
            var pathInVolume = fullPath[volumeRoot.Length..];
            return new MftScanSource(index, fullPath.TrimEnd('\\'), pathInVolume);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            reason = ex is UnauthorizedAccessException
                ? "accès brut au volume refusé (droits administrateur requis)"
                : ex.Message;
            return null;
        }
    }
}
