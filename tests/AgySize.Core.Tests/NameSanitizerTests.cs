using AgySize.Core.Audit.Remediation;
using AgySize.Core.Models;
using Xunit;

namespace AgySize.Core.Tests;

public class NameSanitizerTests
{
    [Fact]
    public void SuggestFixedName_ReplacesInvalidCharactersWithUnderscore()
    {
        var result = NameSanitizer.SuggestFixedName("budget:2024*.xlsx", new[] { AuditIssueType.InvalidCharacterInName }, 255);

        Assert.Equal("budget_2024_.xlsx", result);
    }

    [Fact]
    public void SuggestFixedName_TrimsLeadingAndTrailingSpaces()
    {
        var result = NameSanitizer.SuggestFixedName(" report.txt ", new[] { AuditIssueType.NameStartsOrEndsWithSpace }, 255);

        Assert.Equal("report.txt", result);
    }

    [Fact]
    public void SuggestFixedName_RemovesTrailingPeriod()
    {
        var result = NameSanitizer.SuggestFixedName("dossier.", new[] { AuditIssueType.NameEndsWithPeriod }, 255);

        Assert.Equal("dossier", result);
    }

    [Fact]
    public void SuggestFixedName_CollapsesConsecutivePeriods()
    {
        var result = NameSanitizer.SuggestFixedName("v1..final...docx", new[] { AuditIssueType.ConsecutivePeriodsInName }, 255);

        Assert.Equal("v1.final.docx", result);
    }

    [Fact]
    public void SuggestFixedName_TruncatesOverlyLongNameWhilePreservingExtension()
    {
        var longName = new string('a', 300) + ".docx";

        var result = NameSanitizer.SuggestFixedName(longName, new[] { AuditIssueType.NameTooLong }, 255);

        Assert.NotNull(result);
        Assert.True(result!.Length <= 255);
        Assert.EndsWith(".docx", result);
    }

    [Fact]
    public void SuggestFixedName_CombinesMultipleIssuesInOneRename()
    {
        var result = NameSanitizer.SuggestFixedName(" report:final. ",
            new[] { AuditIssueType.NameStartsOrEndsWithSpace, AuditIssueType.InvalidCharacterInName, AuditIssueType.NameEndsWithPeriod },
            255);

        Assert.Equal("report_final", result);
    }

    [Fact]
    public void SuggestFixedName_ReturnsNullForNonFixableCategories()
    {
        var result = NameSanitizer.SuggestFixedName("CON.txt", new[] { AuditIssueType.ReservedName }, 255);

        Assert.Null(result);
    }

    [Fact]
    public void SuggestFixedName_ReturnsNullWhenNameIsAlreadyCompliant()
    {
        var result = NameSanitizer.SuggestFixedName("report.txt", new[] { AuditIssueType.NameStartsOrEndsWithSpace }, 255);

        Assert.Null(result);
    }
}
