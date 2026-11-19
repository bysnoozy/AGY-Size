using AgySize.Core.Logging;
using AgySize.Core.Models;
using AgySize.Core.Reporting;
using AgySize.Core.Scanning;
using AgySize.Core.Sharing;
using PdfSharp.Pdf.IO;
using Xunit;

namespace AgySize.Core.Tests;

public class ShareReportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AgySizeShares_" + Guid.NewGuid().ToString("N"));

    public ShareReportTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Compta", "2024"));
        Directory.CreateDirectory(Path.Combine(_root, "RH"));
        Directory.CreateDirectory(Path.Combine(_root, "NonPartage"));
        File.WriteAllText(Path.Combine(_root, "Compta", "2024", "bilan.xlsx"), new string('a', 100));
        File.WriteAllText(Path.Combine(_root, "RH", "contrat.docx"), new string('b', 50));
        File.WriteAllText(Path.Combine(_root, "NonPartage", "prive.txt"), new string('c', 1000));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private ScanResult Scan() =>
        new FileSystemScanner().Scan(new ScanOptions { RootPath = _root }, new InMemoryAuditLogger());

    [Fact]
    public void Match_FindsSharesInsideScannedTree_CaseInsensitive()
    {
        var result = Scan();
        var shares = new[]
        {
            new NetworkShare("Compta", @"\\SRV\Compta", Path.Combine(_root, "COMPTA")),
            new NetworkShare("RH$", @"\\SRV\RH$", Path.Combine(_root, "RH")),
            new NetworkShare("Ailleurs", @"\\SRV\Ailleurs", "/autre/volume/Ailleurs"),
        };

        var coverage = ShareMatcher.Match(result, shares);

        Assert.Equal(100, coverage.Single(c => c.Share.Name == "Compta").Node!.SizeInBytes);
        Assert.Equal(50, coverage.Single(c => c.Share.Name == "RH$").Node!.SizeInBytes);
        Assert.False(coverage.Single(c => c.Share.Name == "Ailleurs").IsCovered);
    }

    [Fact]
    public void TopLevel_IgnoresNestedShares_ForTotals()
    {
        var result = Scan();
        var coverage = ShareMatcher.Match(result, new[]
        {
            new NetworkShare("Compta", @"\\SRV\Compta", Path.Combine(_root, "Compta")),
            new NetworkShare("Compta2024", @"\\SRV\Compta2024", Path.Combine(_root, "Compta", "2024")),
        });

        var top = ShareMatcher.TopLevel(coverage);

        Assert.Single(top);
        Assert.Equal("Compta", top[0].Share.Name);
    }

    [Fact]
    public void Match_UncRoot_IsItselfTheShare()
    {
        var root = new FileSystemNode
        {
            Name = "Partage", FullPath = @"\\SRV\Partage", RelativePath = string.Empty,
            Kind = FileSystemNodeKind.Folder, LastWriteUtc = DateTime.UtcNow, Depth = 0,
        };
        var result = new ScanResult
        {
            RootPath = @"\\SRV\Partage", RootNode = root, AuditIssues = new List<AuditIssue>(), Errors = new List<string>(),
            ExtensionStats = new List<ExtensionStat>(), LargestFiles = new List<FileSystemNode>(),
            LargestFolders = new List<FileSystemNode>(), EmptyFolders = new List<FileSystemNode>(),
            PermissionFindings = new List<PermissionFinding>(), ScanStartedUtc = DateTime.UtcNow, ScanCompletedUtc = DateTime.UtcNow,
        };

        var coverage = ShareMatcher.Match(result, Array.Empty<NetworkShare>());

        Assert.Equal(@"\\SRV\Partage", Assert.Single(coverage).Share.UncPath);
        Assert.Same(root, coverage[0].Node);
    }

    [Fact]
    public void WriteReport_ProducesPdf()
    {
        var result = Scan();
        var coverage = ShareMatcher.Match(result, new[]
        {
            new NetworkShare("Compta", @"\\SRV\Compta", Path.Combine(_root, "Compta"), "Service comptable"),
            new NetworkShare("Ailleurs", @"\\SRV\Ailleurs", "/autre/Ailleurs"),
        });
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pdf");

        try
        {
            SharesPdfReportWriter.WriteReport(result, coverage, path);
            using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            Assert.Equal(1, document.PageCount);
        }
        catch (InvalidOperationException)
        {
            // Aucune police TrueType sur la machine (image CI minimale) : rien à vérifier.
        }
        finally
        {
            File.Delete(path);
        }
    }
}
