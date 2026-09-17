namespace AgySize.Core.Models;

public sealed class DuplicateGroup
{
    public required long SizeInBytes { get; init; }

    public required string Hash { get; init; }

    public required IReadOnlyList<FileSystemNode> Files { get; init; }

    /// <summary>Espace récupérable si l'on ne conservait qu'un seul exemplaire du groupe.</summary>
    public long WastedBytes => SizeInBytes * (Files.Count - 1);
}
