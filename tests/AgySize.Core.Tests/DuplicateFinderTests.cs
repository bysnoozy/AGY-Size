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

    [Fact]
    public void FindDuplicates_GroupsIdenticalFilesLargerThanThePartialHashThreshold()
    {
        // Le pré-filtre ne hache que les 64 premiers Ko avant de décider s'il vaut la peine de lire le
        // fichier en entier ; ce test couvre le cas d'un vrai doublon au-delà de ce seuil.
        var content = new string('x', 70_000);
        File.WriteAllText(Path.Combine(_tempRoot, "a.bin"), content);
        File.WriteAllText(Path.Combine(_tempRoot, "b.bin"), content);

        var scanner = new FileSystemScanner();
        var result = scanner.Scan(new ScanOptions { RootPath = _tempRoot }, _logger);

        var groups = DuplicateFinder.FindDuplicates(result.RootNode, _logger);

        var group = Assert.Single(groups);
        Assert.Equal(2, group.Files.Count);
    }

    [Fact]
    public void FindDuplicates_DistinguishesFilesSharingOnlyTheirPartialHashPrefix()
    {
        // Non-régression pour le pré-filtre : deux fichiers identiques sur leurs 64 premiers Ko (donc
        // avec le même hachage partiel) mais différents au-delà ne doivent pas être signalés comme
        // doublons — le hachage complet en phase 2 doit les départager.
        var sharedPrefix = new string('a', 70_000);
        File.WriteAllText(Path.Combine(_tempRoot, "a.bin"), sharedPrefix + "TAIL-A");
        File.WriteAllText(Path.Combine(_tempRoot, "b.bin"), sharedPrefix + "TAIL-B");

        var scanner = new FileSystemScanner();
        var result = scanner.Scan(new ScanOptions { RootPath = _tempRoot }, _logger);

        var groups = DuplicateFinder.FindDuplicates(result.RootNode, _logger);

        Assert.Empty(groups);
    }
}
