using AgySize.Core.Models;

namespace AgySize.Core.Audit.Rules;

/// <summary>
/// Flags file or folder names containing characters SharePoint does not allow.
/// </summary>
public sealed class InvalidCharactersRule : IAuditRule
{
    public IEnumerable<AuditIssue> Evaluate(FileSystemNode entry, ScanOptions options)
    {
        var found = entry.Name
            .Where(c => SharePointLimits.InvalidNameCharacters.Contains(c))
            .Distinct()
            .ToArray();

        if (found.Length > 0)
        {
            var list = string.Join(' ', found.Select(c => $"'{c}'"));
            yield return new AuditIssue
            {
                Type = AuditIssueType.InvalidCharacterInName,
                Severity = AuditSeverity.Blocking,
                RelativePath = entry.RelativePath,
                Description = $"Le nom contient des caractères non autorisés par SharePoint : {list}.",
            };
        }
    }
}
