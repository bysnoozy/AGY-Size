using System.Security.Cryptography;
using AgySize.Core.Logging;
using AgySize.Core.Models;

namespace AgySize.Core.Scanning;

/// <summary>
/// Recherche de doublons à la demande (opération coûteuse, non exécutée pendant le scan initial) :
/// regroupe d'abord par taille, puis calcule un hachage SHA-256 uniquement pour les fichiers dont la
/// taille est partagée par au moins un autre fichier.
/// </summary>
public static class DuplicateFinder
{
    public static IReadOnlyList<DuplicateGroup> FindDuplicates(
        FileSystemNode root,
        IAuditLogger? logger = null,
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

        var groups = new List<DuplicateGroup>();

        foreach (var (size, candidates) in filesBySize)
        {
            if (candidates.Count < 2)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var byHash = new Dictionary<string, List<FileSystemNode>>();
            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string hash;
                try
                {
                    hash = ComputeHash(candidate.FullPath);
                }
                catch (IOException ex)
                {
                    logger?.Warning("Doublons", $"Impossible de lire '{candidate.FullPath}' : {ex.Message}");
                    continue;
                }
                catch (UnauthorizedAccessException ex)
                {
                    logger?.Warning("Doublons", $"Accès refusé à '{candidate.FullPath}' : {ex.Message}");
                    continue;
                }

                if (!byHash.TryGetValue(hash, out var list))
                {
                    list = new List<FileSystemNode>();
                    byHash[hash] = list;
                }

                list.Add(candidate);
            }

            foreach (var (hash, files) in byHash)
            {
                if (files.Count < 2)
                {
                    continue;
                }

                groups.Add(new DuplicateGroup
                {
                    SizeInBytes = size,
                    Hash = hash,
                    Files = files,
                });
            }
        }

        return groups.OrderByDescending(g => g.WastedBytes).ToList();
    }

    private static string ComputeHash(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }
}
