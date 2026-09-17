namespace AgySize.Core.Models;

public sealed class ScanResult
{
    public required string RootPath { get; init; }

    public required FileSystemNode RootNode { get; init; }

    public required DateTime ScanStartedUtc { get; init; }

    public required DateTime ScanCompletedUtc { get; init; }

    /// <summary>Dossiers ou fichiers qui n'ont pas pu être lus (accès refusé, erreur I/O...).</summary>
    public required IReadOnlyList<string> Errors { get; init; }

    public required IReadOnlyList<ExtensionStat> ExtensionStats { get; init; }

    public required IReadOnlyList<FileSystemNode> LargestFiles { get; init; }

    public required IReadOnlyList<FileSystemNode> LargestFolders { get; init; }

    public required IReadOnlyList<FileSystemNode> EmptyFolders { get; init; }

    /// <summary>Vide si <see cref="ScanOptions.AnalyzePermissions"/> était désactivé pour ce scan.</summary>
    public required IReadOnlyList<PermissionFinding> PermissionFindings { get; init; }

    public DateTime? OldestFileModifiedUtc { get; init; }

    public DateTime? NewestFileModifiedUtc { get; init; }
}
