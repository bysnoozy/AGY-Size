using AgySize.Core.Logging;
using AgySize.Core.Models;
using AgySize.Core.Scanning;
using Xunit;

namespace AgySize.Core.Tests;

public class DuplicateFinderTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly InMemoryAuditLogger _logger = new();

    public DuplicateFinderTests()
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
    public void FindDuplicates_GroupsFilesWithIdenticalContent()
    {
        File.WriteAllText(Path.Combine(_tempRoot, "a.txt"), "same content");
        File.WriteAllText(Path.Combine(_tempRoot, "b.txt"), "same content");
        File.WriteAllText(Path.Combine(_tempRoot, "c.txt"), "different!");

        var scanner = new FileSystemScanner();
        var result = scanner.Scan(new ScanOptions { RootPath = _tempRoot }, _logger);

        var groups = DuplicateFinder.FindDuplicates(result.RootNode, _logger);

        var group = Assert.Single(groups);
        Assert.Equal(2, group.Files.Count);
        Assert.Contains(group.Files, f => f.Name == "a.txt");
        Assert.Contains(group.Files, f => f.Name == "b.txt");
    }

    [Fact]
    public void FindDuplicates_IgnoresFilesWithDifferentContentButSameSize()
    {
        File.WriteAllText(Path.Combine(_tempRoot, "a.txt"), "aaaaa");
        File.WriteAllText(Path.Combine(_tempRoot, "b.txt"), "bbbbb");

        var scanner = new FileSystemScanner();
        var result = scanner.Scan(new ScanOptions { RootPath = _tempRoot }, _logger);

        var groups = DuplicateFinder.FindDuplicates(result.RootNode, _logger);

        Assert.Empty(groups);
    }
}
