namespace AgySize.Core.Models;

public enum AuditIssueType
{
    PathTooLong,
    InvalidCharacterInName,
    NameStartsOrEndsWithSpace,
    NameEndsWithPeriod,
    ConsecutivePeriodsInName,
    NameTooLong,
    ReservedName,
    BlockedFileType,
    FileTooLarge,
    FolderTooDeep,
    DuplicateNameDifferingByCase,
}
