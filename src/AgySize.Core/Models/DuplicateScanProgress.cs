namespace AgySize.Core.Models;

public sealed class DuplicateScanProgress
{
    public required int FilesHashed { get; init; }

    public required int TotalFiles { get; init; }
}
