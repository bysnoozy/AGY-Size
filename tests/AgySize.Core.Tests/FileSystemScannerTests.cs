using AgySize.Core.Logging;
using AgySize.Core.Models;
using AgySize.Core.Scanning;
using Xunit;

namespace AgySize.Core.Tests;

public class FileSystemScannerTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly InMemoryAuditLogger _logger = new();

    public FileSystemScannerTests()
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
    public void Scan_AggregatesSizesUpTheTree()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "Sub"));
        File.WriteAllText(Path.Combine(_tempRoot, "a.txt"), new string('a', 10));
        File.WriteAllText(Path.Combine(_tempRoot, "Sub", "b.txt"), new string('b', 20));

        var scanner = new FileSystemScanner();
        var result = scanner.Scan(new ScanOptions { RootPath = _tempRoot }, _logger);

        Assert.Equal(2, result.RootNode.FileCount);
        Assert.Equal(1, result.RootNode.FolderCount);
        Assert.Equal(30, result.RootNode.SizeInBytes);

        var sub = result.RootNode.Children.Single(c => c.Name == "Sub");
        Assert.Equal(20, sub.SizeInBytes);
        Assert.Equal(1, sub.FileCount);
    }

    [Fact]
    public void Scan_TracksLargestFilesAndFolders()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "Big"));
        File.WriteAllText(Path.Combine(_tempRoot, "small.txt"), new string('a', 10));
        File.WriteAllText(Path.Combine(_tempRoot, "Big", "big.txt"), new string('a', 1000));

        var scanner = new FileSystemScanner();
        var result = scanner.Scan(new ScanOptions { RootPath = _tempRoot }, _logger);

        Assert.Equal("Big/big.txt", result.LargestFiles.First().RelativePath);
        Assert.Equal("Big", result.LargestFolders.First().RelativePath);
    }

    [Fact]
    public void Scan_DetectsEmptyFolders()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "Empty"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "NotEmpty"));
        File.WriteAllText(Path.Combine(_tempRoot, "NotEmpty", "file.txt"), "content");

        var scanner = new FileSystemScanner();
        var result = scanner.Scan(new ScanOptions { RootPath = _tempRoot }, _logger);

        Assert.Contains(result.EmptyFolders, f => f.RelativePath == "Empty");
        Assert.DoesNotContain(result.EmptyFolders, f => f.RelativePath == "NotEmpty");
    }

    [Fact]
    public void Scan_ComputesExtensionStats()
    {
        File.WriteAllText(Path.Combine(_tempRoot, "a.txt"), new string('a', 10));
        File.WriteAllText(Path.Combine(_tempRoot, "b.txt"), new string('a', 20));
        File.WriteAllText(Path.Combine(_tempRoot, "c.log"), new string('a', 5));

        var scanner = new FileSystemScanner();
        var result = scanner.Scan(new ScanOptions { RootPath = _tempRoot }, _logger);

        var txtStats = result.ExtensionStats.Single(e => e.Extension == ".txt");
        Assert.Equal(2, txtStats.FileCount);
        Assert.Equal(30, txtStats.TotalSizeInBytes);
    }

    [Fact]
    public void Scan_ThrowsWhenRootDoesNotExist()
    {
        var scanner = new FileSystemScanner();
        var options = new ScanOptions { RootPath = Path.Combine(_tempRoot, "does-not-exist") };

        Assert.Throws<DirectoryNotFoundException>(() => scanner.Scan(options, _logger));
    }
}
