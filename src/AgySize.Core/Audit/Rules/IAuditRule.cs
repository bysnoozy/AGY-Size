using AgySize.Core.Models;

namespace AgySize.Core.Audit.Rules;

public interface IAuditRule
{
    IEnumerable<AuditIssue> Evaluate(FileSystemNode entry, ScanOptions options);
}
