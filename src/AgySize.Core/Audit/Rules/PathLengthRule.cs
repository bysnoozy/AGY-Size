using AgySize.Core.Models;

namespace AgySize.Core.Audit.Rules;

/// <summary>
/// Flags entries whose relative path would exceed the character budget left for a SharePoint URL
/// once the destination site and library prefix is accounted for.
/// </summary>
public sealed class PathLengthRule : IAuditRule
{
    public IEnumerable<AuditIssue> Evaluate(FileSystemNode entry, ScanOptions options)
    {
        var limit = options.EffectiveMaxRelativePathLength;

        if (entry.RelativePath.Length > limit)
        {
            yield return new AuditIssue
            {
                Type = AuditIssueType.PathTooLong,
                Severity = AuditSeverity.Blocking,
                RelativePath = entry.RelativePath,
                Description = $"Le chemin relatif comporte {entry.RelativePath.Length} caractères, ce qui " +
                    $"dépasse le budget de {limit} caractères restant après réservation de " +
                    $"{options.ReservedUrlPrefixLength} caractères pour l'URL du site et de la bibliothèque " +
                    $"SharePoint de destination (limite globale : {options.MaxFullUrlLength} caractères).",
            };
        }
    }
}
