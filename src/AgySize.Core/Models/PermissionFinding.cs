namespace AgySize.Core.Models;

public sealed class PermissionFinding
{
    public required string RelativePath { get; init; }

    public required FileSystemNodeKind Kind { get; init; }

    public required PermissionSeverity Severity { get; init; }

    public required PermissionFindingCategory Category { get; init; }

    public required string Description { get; init; }

    public string? IdentityName { get; init; }

    public string? Rights { get; init; }
}
