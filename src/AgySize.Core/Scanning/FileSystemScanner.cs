using System.Diagnostics;
using AgySize.Core.Logging;
using AgySize.Core.Models;
using AgySize.Core.Security;

namespace AgySize.Core.Scanning;

/// <summary>
/// Parcourt une arborescence et construit un arbre de <see cref="FileSystemNode"/> avec les tailles
/// agrégées récursivement (façon TreeSize), tout en collectant la volumétrie par extension, les plus
/// gros fichiers/dossiers, les dossiers vides et, si demandé, les constats sur les droits NTFS.
/// </summary>
public sealed class FileSystemScanner
{
    private const int TopListSize = 50;
    private const int ProgressReportIntervalMs = 250;

    public ScanResult Scan(
        ScanOptions options,
        IAuditLogger logger,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var rootInfo = new DirectoryInfo(options.RootPath);
        if (!rootInfo.Exists)
        {
            throw new DirectoryNotFoundException($"Dossier introuvable : {options.RootPath}");
        }

        var startedUtc = DateTime.UtcNow;
        var errors = new List<string>();
        var extensionStats = new Dictionary<string, (long Count, long Size)>(StringComparer.OrdinalIgnoreCase);
        var largestFiles = new List<FileSystemNode>(TopListSize + 1);
        var largestFolders = new List<FileSystemNode>(TopListSize + 1);
        var emptyFolders = new List<FileSystemNode>();
        var permissionFindings = new List<PermissionFinding>();

        long totalFiles = 0;
        long totalFolders = 0;
        DateTime? oldest = null;
        DateTime? newest = null;

        var stopwatch = Stopwatch.StartNew();
        long lastReportMs = 0;

        var analyzePermissions = options.AnalyzePermissions && OperatingSystem.IsWindows();
        if (options.AnalyzePermissions && !OperatingSystem.IsWindows())
        {
            logger.Warning("Scanner", "L'analyse des droits NTFS a été demandée mais n'est disponible que sous Windows ; elle est ignorée.");
        }

        logger.Info("Scanner", $"Démarrage de l'analyse de '{options.RootPath}'.");

        FileSystemNode ScanDirectory(DirectoryInfo directory, string relativePath, int depth)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var node = new FileSystemNode
            {
                Name = directory.Name,
                FullPath = directory.FullName,
                RelativePath = relativePath,
                Kind = FileSystemNodeKind.Folder,
                LastWriteUtc = directory.LastWriteTimeUtc,
                Depth = depth,
            };

            totalFolders++;

            if (analyzePermissions)
            {
                permissionFindings.AddRange(PermissionsAnalyzer.AnalyzeFolder(node));
            }

            FileSystemInfo[] children;
            try
            {
                children = directory.GetFileSystemInfos();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                var message = $"Impossible de lire '{directory.FullName}' : {ex.Message}";
                errors.Add(message);
                logger.Warning("Scanner", message);
                return node;
            }

            foreach (var child in children)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var childRelativePath = relativePath.Length == 0 ? child.Name : $"{relativePath}/{child.Name}";

                if (child is DirectoryInfo subDirectory)
                {
                    var childNode = ScanDirectory(subDirectory, childRelativePath, depth + 1);
                    childNode.Parent = node;
                    node.Children.Add(childNode);
                    node.SizeInBytes += childNode.SizeInBytes;
                    node.FileCount += childNode.FileCount;
                    node.FolderCount += childNode.FolderCount + 1;

                    InsertTop(largestFolders, childNode);
                    if (childNode.IsEmptyFolder)
                    {
                        emptyFolders.Add(childNode);
                    }
                }
                else if (child is FileInfo file)
                {
                    long size;
                    DateTime lastWrite;
                    try
                    {
                        size = file.Length;
                        lastWrite = file.LastWriteTimeUtc;
                    }
                    catch (IOException ex)
                    {
                        var message = $"Impossible de lire '{file.FullName}' : {ex.Message}";
                        errors.Add(message);
                        logger.Warning("Scanner", message);
                        continue;
                    }

                    var fileNode = new FileSystemNode
                    {
                        Name = child.Name,
                        FullPath = file.FullName,
                        RelativePath = childRelativePath,
                        Kind = FileSystemNodeKind.File,
                        SizeInBytes = size,
                        LastWriteUtc = lastWrite,
                        Depth = depth + 1,
                        Parent = node,
                    };

                    node.Children.Add(fileNode);
                    node.SizeInBytes += size;
                    node.FileCount += 1;

                    totalFiles++;

                    var rawExtension = Path.GetExtension(fileNode.Name);
                    var extension = string.IsNullOrEmpty(rawExtension) ? "(sans extension)" : rawExtension.ToLowerInvariant();
                    extensionStats.TryGetValue(extension, out var stats);
                    extensionStats[extension] = (stats.Count + 1, stats.Size + size);

                    if (oldest is null || lastWrite < oldest)
                    {
                        oldest = lastWrite;
                    }

                    if (newest is null || lastWrite > newest)
                    {
                        newest = lastWrite;
                    }

                    InsertTop(largestFiles, fileNode);
                }

                if (stopwatch.ElapsedMilliseconds - lastReportMs >= ProgressReportIntervalMs)
                {
                    lastReportMs = stopwatch.ElapsedMilliseconds;
                    progress?.Report(new ScanProgress
                    {
                        FilesScanned = totalFiles,
                        FoldersScanned = totalFolders,
                        CurrentPath = childRelativePath,
                    });
                }
            }

            node.Children.Sort((a, b) => b.SizeInBytes.CompareTo(a.SizeInBytes));
            return node;
        }

        var root = ScanDirectory(rootInfo, string.Empty, 0);
        if (root.IsEmptyFolder)
        {
            emptyFolders.Insert(0, root);
        }

        progress?.Report(new ScanProgress { FilesScanned = totalFiles, FoldersScanned = totalFolders, CurrentPath = null });

        logger.Info(
            "Scanner",
            $"Analyse terminée : {totalFiles:N0} fichier(s), {totalFolders:N0} dossier(s), {errors.Count:N0} erreur(s) de lecture.");

        return new ScanResult
        {
            RootPath = options.RootPath,
            RootNode = root,
            ScanStartedUtc = startedUtc,
            ScanCompletedUtc = DateTime.UtcNow,
            Errors = errors,
            ExtensionStats = extensionStats
                .Select(kvp => new ExtensionStat(kvp.Key, kvp.Value.Count, kvp.Value.Size))
                .OrderByDescending(e => e.TotalSizeInBytes)
                .ToList(),
            LargestFiles = largestFiles,
            LargestFolders = largestFolders,
            EmptyFolders = emptyFolders,
            PermissionFindings = permissionFindings,
            OldestFileModifiedUtc = oldest,
            NewestFileModifiedUtc = newest,
        };
    }

    private static void InsertTop(List<FileSystemNode> list, FileSystemNode node)
    {
        if (list.Count < TopListSize)
        {
            list.Add(node);
            list.Sort((a, b) => b.SizeInBytes.CompareTo(a.SizeInBytes));
            return;
        }

        if (node.SizeInBytes <= list[^1].SizeInBytes)
        {
            return;
        }

        list[^1] = node;
        list.Sort((a, b) => b.SizeInBytes.CompareTo(a.SizeInBytes));
    }
}
