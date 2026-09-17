namespace AgySize.Core.Models;

public sealed class ScanProgress
{
    public required long FilesScanned { get; init; }

    public required long FoldersScanned { get; init; }

    public string? CurrentPath { get; init; }
}
