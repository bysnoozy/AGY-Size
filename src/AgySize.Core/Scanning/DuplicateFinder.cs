using System.Collections.Concurrent;
using System.Security.Cryptography;
using AgySize.Core.Logging;
using AgySize.Core.Models;

namespace AgySize.Core.Scanning;

/// <summary>
/// Recherche de doublons à la demande (opération coûteuse, non exécutée pendant le scan initial).
/// Trois filtres successifs, du moins cher au plus cher, pour éviter de hacher intégralement des
/// fichiers qui n'ont aucune chance d'être des doublons :
/// 1. regroupement par taille (deux fichiers de tailles différentes ne peuvent pas être identiques) ;
/// 2. hachage partiel (les 64 premiers Ko) en parallèle, pour écarter à moindre coût les fichiers qui
///    partagent une taille par coïncidence sans être des doublons — cas fréquent sur de grosses
///    arborescences (fichiers vides remplis de zéros jusqu'à une taille standard, ressources
///    d'installateurs de même gabarit...) ;
/// 3. hachage SHA-256 complet, également en parallèle, uniquement pour les fichiers qui partagent à
///    la fois leur taille et leur hachage partiel.
/// </summary>
public static class DuplicateFinder
{
    private const int PartialHashBytes = 64 * 1024;

    public static IReadOnlyList<DuplicateGroup> FindDuplicates(
        FileSystemNode root,
        IAuditLogger? logger = null,
        IProgress<DuplicateScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var filesBySize = new Dictionary<long, List<FileSystemNode>>();
        foreach (var file in root.Files())
        {
            if (file.SizeInBytes == 0)
            {
                continue;
            }

            if (!filesBySize.TryGetValue(file.SizeInBytes, out var list))
            {
                list = new List<FileSystemNode>();
                filesBySize[file.SizeInBytes] = list;
            }

            list.Add(file);
        }

        var sizeCandidates = filesBySize.Values.Where(l => l.Count >= 2).SelectMany(l => l).ToList();
        var totalToHash = sizeCandidates.Count;
        var hashedSoFar = 0;

        void ReportProgress()
        {
            if (progress is null)
            {
                return;
            }

            var done = Interlocked.Increment(ref hashedSoFar);
            progress.Report(new DuplicateScanProgress { FilesHashed = done, TotalFiles = totalToHash });
        }

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
        };

        var byPartialHash = new ConcurrentDictionary<(long Size, string PartialHash), ConcurrentBag<FileSystemNode>>();
        Parallel.ForEach(sizeCandidates, parallelOptions, file =>
        {
            try
            {
                var partialHash = ComputeHash(file.FullPath, PartialHashBytes);
                byPartialHash.GetOrAdd((file.SizeInBytes, partialHash), _ => new ConcurrentBag<FileSystemNode>()).Add(file);
            }
            catch (IOException ex)
            {
                logger?.Warning("Doublons", $"Impossible de lire '{file.FullPath}' : {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                logger?.Warning("Doublons", $"Accès refusé à '{file.FullPath}' : {ex.Message}");
            }
            finally
            {
                ReportProgress();
            }
        });

        var fullHashCandidates = byPartialHash.Values.Where(b => b.Count >= 2).SelectMany(b => b).ToList();

        var byFullHash = new ConcurrentDictionary<string, ConcurrentBag<FileSystemNode>>();
        Parallel.ForEach(fullHashCandidates, parallelOptions, file =>
        {
            try
            {
                var hash = ComputeHash(file.FullPath);
                byFullHash.GetOrAdd(hash, _ => new ConcurrentBag<FileSystemNode>()).Add(file);
            }
            catch (IOException ex)
            {
                logger?.Warning("Doublons", $"Impossible de lire '{file.FullPath}' : {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                logger?.Warning("Doublons", $"Accès refusé à '{file.FullPath}' : {ex.Message}");
            }
        });

        var groups = new List<DuplicateGroup>();
        foreach (var (hash, bag) in byFullHash)
        {
            var files = bag.ToList();
            if (files.Count < 2)
            {
                continue;
            }

            groups.Add(new DuplicateGroup { SizeInBytes = files[0].SizeInBytes, Hash = hash, Files = files });
        }

        return groups.OrderByDescending(g => g.WastedBytes).ToList();
    }

    /// <summary>
    /// Hache le fichier entier, ou seulement ses <paramref name="maxBytes"/> premiers octets si fourni
    /// (utilisé pour le pré-filtre : un fichier plus petit que <paramref name="maxBytes"/> est alors
    /// haché intégralement, ce qui est correct — juste redondant avec la phase de hachage complet, pour
    /// un coût négligeable vu la petite taille du fichier).
    /// </summary>
    private static string ComputeHash(string path, int? maxBytes = null)
    {
        using var stream = File.OpenRead(path);

        if (maxBytes is null)
        {
            return Convert.ToHexString(SHA256.HashData(stream));
        }

        var length = (int)Math.Min(maxBytes.Value, stream.Length);
        var buffer = new byte[length];
        var read = 0;
        while (read < length)
        {
            var n = stream.Read(buffer, read, length - read);
            if (n == 0)
            {
                break;
            }

            read += n;
        }

        return Convert.ToHexString(SHA256.HashData(buffer.AsSpan(0, read)));
    }
}
