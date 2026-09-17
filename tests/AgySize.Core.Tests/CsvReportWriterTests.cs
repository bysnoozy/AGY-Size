using AgySize.Core.Logging;
using AgySize.Core.Models;
using AgySize.Core.Reporting;
using AgySize.Core.Scanning;
using Xunit;

namespace AgySize.Core.Tests;

public class CsvReportWriterTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly InMemoryAuditLogger _logger = new();

    public CsvReportWriterTests()
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

    [Fact]
    public void WriteSizeReport_ContainsOneRowPerNode()
    {
        File.WriteAllText(Path.Combine(_tempRoot, "a.txt"), new string('a', 10));

        var scanner = new FileSystemScanner();
        var result = scanner.Scan(new ScanOptions { RootPath = _tempRoot }, _logger);

        var csvPath = Path.Combine(_tempRoot, "report.csv");
        CsvReportWriter.WriteSizeReport(result, csvPath);

        var lines = File.ReadAllLines(csvPath);

        Assert.Equal("Type;Chemin;Taille (octets);Dernière modification", lines[0]);
        Assert.Contains(lines, l => l.Contains("a.txt") && l.StartsWith("Fichier"));
        Assert.Contains(lines, l => l.StartsWith("Dossier;(racine)"));
    }
}
