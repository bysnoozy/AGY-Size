namespace AgySize.Core.Models;

public sealed class LogEntry
{
    public required DateTime TimestampUtc { get; init; }

    public required LogLevel Level { get; init; }

    public required string Source { get; init; }

    public required string Message { get; init; }
}
