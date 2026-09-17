using AgySize.Core.Logging;
using AgySize.Core.Models;
using AgySize.Core.Operations;
using AgySize.Core.Scanning;
using Xunit;

namespace AgySize.Core.Tests;

public class FileOperationsTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly InMemoryAuditLogger _logger = new();

    public FileOperationsTests()
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
    public void Delete_UpdatesAncestorSizesAndRemovesFromTree()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "Sub"));
        File.WriteAllText(Path.Combine(_tempRoot, "Sub", "file.txt"), new string('a', 100));

        var scanner = new FileSystemScanner();
        var result = scanner.Scan(new ScanOptions { RootPath = _tempRoot }, _logger);

        var sub = result.RootNode.Children.Single(c => c.Name == "Sub");
        var file = sub.Children.Single(c => c.Name == "file.txt");

        FileOperations.Delete(file, useRecycleBin: false);

        Assert.False(File.Exists(Path.Combine(_tempRoot, "Sub", "file.txt")));
        Assert.Empty(sub.Children);
        Assert.Equal(0, sub.SizeInBytes);
        Assert.Equal(0, sub.FileCount);
        Assert.Equal(0, result.RootNode.SizeInBytes);
        Assert.Equal(0, result.RootNode.FileCount);
    }

    [Fact]
    public void Move_UpdatesAncestorSizesAndRemovesFromSourceTree()
    {
        File.WriteAllText(Path.Combine(_tempRoot, "file.txt"), new string('a', 50));
        var destination = Path.Combine(_tempRoot, "Destination");
        Directory.CreateDirectory(destination);

        var scanner = new FileSystemScanner();
        var result = scanner.Scan(new ScanOptions { RootPath = _tempRoot }, _logger);

        var file = result.RootNode.Children.Single(c => c.Name == "file.txt" && c.Kind == FileSystemNodeKind.File);

        FileOperations.Move(file, destination);

        Assert.True(File.Exists(Path.Combine(destination, "file.txt")));
        Assert.DoesNotContain(result.RootNode.Children, c => c.Name == "file.txt");
        Assert.Equal(0, result.RootNode.FileCount);
    }
}
