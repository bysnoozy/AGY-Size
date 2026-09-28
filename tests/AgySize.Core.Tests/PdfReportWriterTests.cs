using System.Text;
using AgySize.Core.Models;
using AgySize.Core.Reporting;
using PdfSharp.Pdf.IO;
using Xunit;

namespace AgySize.Core.Tests;

public class PdfReportWriterTests
{
    [Fact]
    public void WriteClientReport_ProducesValidPdf_EvenWithUnbreakableLongPaths()
    {
        if (!SystemFontAvailable())
        {
            // No TrueType font on this machine (e.g. a minimal CI image); PdfSharp has nothing to
            // embed, so there is nothing this test can verify.
            return;
        }

        var result = BuildSampleResult();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pdf");

        try
        {
            // Regression test: a relative path with no spaces (e.g. deep nesting, or a single very
            // long file name) must not throw and must not overflow the page - see ShortenPath and
            // PdfLayoutBuilder.SplitOversizedWord.
            PdfReportWriter.WriteClientReport(result, path);

            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 0);
            Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));

            using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            Assert.True(document.PageCount >= 1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static bool SystemFontAvailable()
    {
        string[] candidates =
        {
            @"C:\Windows\Fonts\segoeui.ttf",
            @"C:\Windows\Fonts\arial.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
            "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf",
            "/Library/Fonts/Arial.ttf",
            "/System/Library/Fonts/Supplemental/Arial.ttf",
        };

        return candidates.Any(File.Exists);
    }

    private static ScanResult BuildSampleResult()
    {
        var deepPath = string.Join('/', Enumerable.Range(1, 40).Select(i => $"dossier-{i}"));
        var oneWordFileName = new string('a', 90) + ".docx";

        var issues = new List<AuditIssue>
        {
            MakeIssue(AuditIssueType.PathTooLong, AuditSeverity.Blocking, $"{deepPath}/{oneWordFileName}"),
            MakeIssue(AuditIssueType.FolderTooDeep, AuditSeverity.Warning, deepPath),
            MakeIssue(AuditIssueType.BlockedFileType, AuditSeverity.Warning, "installer.exe"),
            MakeIssue(AuditIssueType.BlockedFileType, AuditSeverity.Warning, "setup.msi"),
        };

        var root = new FileSystemNode
        {
            Name = "dossier",
            FullPath = "/exemple/dossier",
            RelativePath = string.Empty,
            Kind = FileSystemNodeKind.Folder,
            LastWriteUtc = DateTime.UtcNow,
            Depth = 0,
        };
        root.SizeInBytes = 12_345;
        root.FileCount = 10;
        root.FolderCount = 41;

        return new ScanResult
        {
            RootPath = "/exemple/dossier",
            RootNode = root,
            AuditIssues = issues,
            Errors = new List<string>(),
            ExtensionStats = new List<ExtensionStat>(),
            LargestFiles = new List<FileSystemNode>(),
            LargestFolders = new List<FileSystemNode>(),
            EmptyFolders = new List<FileSystemNode>(),
            PermissionFindings = new List<PermissionFinding>(),
            ScanStartedUtc = DateTime.UtcNow.AddSeconds(-1),
            ScanCompletedUtc = DateTime.UtcNow,
        };
    }

    private static AuditIssue MakeIssue(AuditIssueType type, AuditSeverity severity, string relativePath) => new()
    {
        Type = type,
        Severity = severity,
        RelativePath = relativePath,
        Description = "Description technique.",
    };
}
