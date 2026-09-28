using AgySize.Core.Models;

namespace AgySize.Core.Audit.Rules;

/// <summary>
/// Flags sibling files/folders whose names are identical once case is ignored. SharePoint (and
/// OneDrive) treat names as case-insensitive, so such siblings collide even though they can coexist
/// on a case-sensitive source file share. Unlike the other rules, this one needs the full set of
/// siblings in a folder rather than a single entry, so it is invoked separately by the scanner once
/// per directory instead of through <see cref="IAuditRule"/>.
/// </summary>
public static class DuplicateNameRule
{
    public static IEnumerable<AuditIssue> Evaluate(IReadOnlyList<FileSystemNode> siblings)
    {
        var seen = new Dictionary<string, FileSystemNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in siblings)
        {
            if (seen.TryGetValue(entry.Name, out var first))
            {
                yield return new AuditIssue
                {
                    Type = AuditIssueType.DuplicateNameDifferingByCase,
                    Severity = AuditSeverity.Blocking,
                    RelativePath = entry.RelativePath,
                    Description = $"'{entry.Name}' entre en collision avec '{first.Name}' du même dossier une " +
                        "fois la casse ignorée ; SharePoint considère ces noms comme identiques.",
                };
            }
            else
            {
                seen[entry.Name] = entry;
            }
        }
    }
}
