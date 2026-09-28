namespace AgySize.Core.Models;

/// <summary>
/// Anomalie de compatibilité avec une migration SharePoint Online / OneDrive (chemin trop long,
/// caractère interdit, type de fichier bloqué...), détectée pendant le scan par les règles de
/// <see cref="Audit.Rules"/>.
/// </summary>
public sealed class AuditIssue
{
    public required AuditIssueType Type { get; init; }

    public required AuditSeverity Severity { get; init; }

    public required string RelativePath { get; init; }

    public required string Description { get; init; }
}
