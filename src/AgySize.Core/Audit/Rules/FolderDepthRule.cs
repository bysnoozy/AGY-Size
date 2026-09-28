using AgySize.Core.Models;

namespace AgySize.Core.Audit.Rules;

/// <summary>
/// Flags folders nested beyond a configurable depth threshold. This is a heuristic warning, not a
/// hard SharePoint limit: deep nesting is simply a common cause of path-length failures.
/// </summary>
public sealed class FolderDepthRule : IAuditRule
{
    public IEnumerable<AuditIssue> Evaluate(FileSystemNode entry, ScanOptions options)
    {
        if (entry.Kind != FileSystemNodeKind.Folder)
        {
            yield break;
        }

        if (entry.Depth > options.MaxFolderDepth)
        {
            yield return new AuditIssue
            {
                Type = AuditIssueType.FolderTooDeep,
                Severity = AuditSeverity.Warning,
                RelativePath = entry.RelativePath,
                Description = $"Le dossier est imbriqué sur {entry.Depth} niveaux, au-delà du seuil d'alerte " +
                    $"configuré de {options.MaxFolderDepth}. Une imbrication profonde est une cause fréquente " +
                    "de dépassement de longueur de chemin lors de la migration.",
            };
        }
    }
}
