using AgySize.Core.Logging;
using AgySize.Core.Models;
using AgySize.Core.Scanning;
using Xunit;

namespace AgySize.Core.Tests;

/// <summary>
/// Tests de l'audit de compatibilité SharePoint (AgySize.Core.Audit.Rules), intégré au scan standard.
/// Certains cas ci-dessous sont des non-régressions pour des bugs corrigés au moment du portage depuis
/// l'ancien projet AuditFiles : voir <see cref="ReservedName_DoesNotFlagFileNamedWithLockExtension"/>,
/// <see cref="BlockedFileType_FlagsPowerShellAndWebExtensions"/> et
/// <see cref="ReservedName_FlagsFormsFolderOnlyAtRoot"/>.
/// </summary>
public class AuditRulesTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly InMemoryAuditLogger _logger = new();
    private readonly FileSystemScanner _scanner = new();

    public AuditRulesTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "AgySizeTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private ScanResult Scan(ScanOptions? options = null) =>
        _scanner.Scan(options ?? new ScanOptions { RootPath = _tempRoot }, _logger);

    [Fact]
    public void PathTooLong_FlagsEntryBeyondBudget()
    {
        const string name = "this-name-is-long-enough-to-exceed-the-tiny-test-budget.txt";
        File.WriteAllText(Path.Combine(_tempRoot, name), "x");

        var result = Scan(new ScanOptions { RootPath = _tempRoot, MaxFullUrlLength = 30, ReservedUrlPrefixLength = 20 });

        Assert.Contains(result.AuditIssues, i => i.Type == AuditIssueType.PathTooLong && i.RelativePath == name);
    }

    [Fact]
    public void InvalidCharactersRule_FlagsForbiddenCharacters()
    {
        File.WriteAllText(Path.Combine(_tempRoot, "brace{name}.txt"), "x");

        var result = Scan();

        Assert.Contains(result.AuditIssues, i => i.Type == AuditIssueType.InvalidCharacterInName && i.RelativePath == "brace{name}.txt");
    }

    [Fact]
    public void InvalidNameRule_AllowsHashAndPercent()
    {
        // '#' et '%' sont autorisés par défaut par SharePoint/OneDrive depuis une mise à jour
        // Microsoft ; ils ne doivent pas apparaître dans les caractères interdits.
        File.WriteAllText(Path.Combine(_tempRoot, "budget#2024%.txt"), "x");

        var result = Scan();

        Assert.DoesNotContain(result.AuditIssues, i => i.RelativePath == "budget#2024%.txt");
    }

    [Fact]
    public void InvalidNameRule_FlagsTrailingSpaceAndPeriod()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "trailing dot."));
        // Le nom complet (extension comprise) doit se terminer par un espace : "report .txt" se
        // termine par 't', pas par un espace, seul "report.txt " (espace après l'extension) qualifie.
        File.WriteAllText(Path.Combine(_tempRoot, "report.txt "), "x");

        var result = Scan();

        Assert.Contains(result.AuditIssues, i => i.Type == AuditIssueType.NameEndsWithPeriod && i.RelativePath == "trailing dot.");
        Assert.Contains(result.AuditIssues, i => i.Type == AuditIssueType.NameStartsOrEndsWithSpace && i.RelativePath == "report.txt ");
    }

    [Fact]
    public void ReservedName_FlagsWindowsDeviceNameEvenWithExtension()
    {
        File.WriteAllText(Path.Combine(_tempRoot, "CON.txt"), "x");

        var result = Scan();

        Assert.Contains(result.AuditIssues, i => i.Type == AuditIssueType.ReservedName && i.RelativePath == "CON.txt");
    }

    [Fact]
    public void ReservedName_DoesNotFlagFileNamedWithLockExtension()
    {
        // Non-régression : ".lock" est réservé en tant que nom EXACT (le fichier système SharePoint),
        // mais "notes.lock" ne doit pas être confondu avec lui via un retrait naïf de l'extension
        // (Path.GetFileNameWithoutExtension(".lock.txt") renvoyait ".lock").
        File.WriteAllText(Path.Combine(_tempRoot, "notes.lock"), "x");
        File.WriteAllText(Path.Combine(_tempRoot, ".lock.txt"), "x");

        var result = Scan();

        Assert.DoesNotContain(result.AuditIssues, i => i.Type == AuditIssueType.ReservedName && i.RelativePath == "notes.lock");
        Assert.DoesNotContain(result.AuditIssues, i => i.Type == AuditIssueType.ReservedName && i.RelativePath == ".lock.txt");
    }

    [Fact]
    public void ReservedName_FlagsExactLockFileName()
    {
        File.WriteAllText(Path.Combine(_tempRoot, ".lock"), "x");

        var result = Scan();

        Assert.Contains(result.AuditIssues, i => i.Type == AuditIssueType.ReservedName && i.RelativePath == ".lock");
    }

    [Fact]
    public void ReservedName_FlagsFormsFolderOnlyAtRoot()
    {
        // Non-régression : Microsoft documente qu'un dossier "Forms" à la racine d'une bibliothèque
        // entre en conflit avec le dossier système que SharePoint y crée ; ce n'est vrai qu'au premier
        // niveau, pas pour un sous-dossier plus profond du même nom.
        Directory.CreateDirectory(Path.Combine(_tempRoot, "Forms"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "Sub", "Forms"));

        var result = Scan();

        Assert.Contains(result.AuditIssues, i => i.Type == AuditIssueType.ReservedName && i.RelativePath == "Forms");
        Assert.DoesNotContain(result.AuditIssues, i => i.Type == AuditIssueType.ReservedName && i.RelativePath == "Sub/Forms");
    }

    [Fact]
    public void BlockedFileType_FlagsPowerShellAndWebExtensions()
    {
        // Non-régression : ces extensions figurent dans la liste des types bloqués publiée par
        // Microsoft et avaient été supprimées par erreur lors d'une précédente mise à jour.
        File.WriteAllText(Path.Combine(_tempRoot, "deploy.ps1"), "x");
        File.WriteAllText(Path.Combine(_tempRoot, "handler.ashx"), "x");
        File.WriteAllText(Path.Combine(_tempRoot, "web.config"), "x");

        var result = Scan();

        Assert.Contains(result.AuditIssues, i => i.Type == AuditIssueType.BlockedFileType && i.RelativePath == "deploy.ps1");
        Assert.Contains(result.AuditIssues, i => i.Type == AuditIssueType.BlockedFileType && i.RelativePath == "handler.ashx");
        Assert.Contains(result.AuditIssues, i => i.Type == AuditIssueType.BlockedFileType && i.RelativePath == "web.config");
    }

    [Fact]
    public void FileSizeRule_FlagsFileAboveConfiguredLimit()
    {
        File.WriteAllText(Path.Combine(_tempRoot, "big.bin"), new string('a', 1000));

        var result = Scan(new ScanOptions { RootPath = _tempRoot, MaxFileSizeInBytes = 100 });

        Assert.Contains(result.AuditIssues, i => i.Type == AuditIssueType.FileTooLarge && i.RelativePath == "big.bin");
    }

    [Fact]
    public void FolderDepthRule_FlagsFolderBeyondConfiguredDepth()
    {
        var deep = Path.Combine(_tempRoot, "a", "b", "c");
        Directory.CreateDirectory(deep);

        var result = Scan(new ScanOptions { RootPath = _tempRoot, MaxFolderDepth = 2 });

        Assert.Contains(result.AuditIssues, i => i.Type == AuditIssueType.FolderTooDeep && i.RelativePath == "a/b/c");
        Assert.DoesNotContain(result.AuditIssues, i => i.Type == AuditIssueType.FolderTooDeep && i.RelativePath == "a/b");
    }

    [Fact]
    public void DuplicateNameRule_FlagsSiblingsDifferingOnlyByCase()
    {
        File.WriteAllText(Path.Combine(_tempRoot, "Report.docx"), "x");
        Directory.CreateDirectory(Path.Combine(_tempRoot, "REPORT.docx"));

        var result = Scan();

        Assert.Contains(result.AuditIssues, i => i.Type == AuditIssueType.DuplicateNameDifferingByCase);
    }

    [Fact]
    public void Scan_DoesNotAuditTheRootFolderItself()
    {
        // La racine scannée (chemin relatif vide) ne devient jamais un dossier de la bibliothèque de
        // destination : son propre nom ne doit donc jamais être audité, même s'il contient un
        // caractère normalement interdit.
        var weirdRoot = Path.Combine(_tempRoot, "weird{root}");
        Directory.CreateDirectory(weirdRoot);

        var result = Scan(new ScanOptions { RootPath = weirdRoot });

        Assert.Empty(result.AuditIssues);
    }
}
