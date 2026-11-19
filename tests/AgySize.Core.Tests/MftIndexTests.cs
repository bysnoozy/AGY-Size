using System.IO.Compression;
using AgySize.Core.Logging;
using AgySize.Core.Models;
using AgySize.Core.Scanning;
using AgySize.Core.Scanning.Mft;
using Xunit;

namespace AgySize.Core.Tests;

/// <summary>
/// Lecture directe de la MFT sur une image NTFS réelle (Fixtures/ntfs-sample.img.gz), contenant :
/// Partage/petit.txt (3 o, daté du 2019-03-04 05:06:07 UTC, avec un second lien physique
/// Partage/Compta 2024/lien.txt), Partage/Compta 2024/gros.bin (200 000 o, non résident),
/// Partage/Compta 2024/Factures/f1.pdf (10 o), Partage/Répertoire accentué/été.docx (1 o),
/// Partage/Vide (dossier vide), Partage/creux.vhd (fichier creux de 3 000 000 000 o) et un
/// fichier supprimé (Partage/supprime.txt) qui ne doit pas apparaître.
/// </summary>
public class MftIndexTests : IDisposable
{
    private readonly string _imagePath = Path.Combine(Path.GetTempPath(), $"agysize-ntfs-{Guid.NewGuid():N}.img");

    public MftIndexTests()
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ntfs-sample.img.gz");
        using var input = new GZipStream(File.OpenRead(fixture), CompressionMode.Decompress);
        using var output = File.Create(_imagePath);
        input.CopyTo(output);
    }

    public void Dispose() => File.Delete(_imagePath);

    [Fact]
    public void ReadVolume_ListsFilesWithSizesAndNames()
    {
        var index = MftIndex.ReadVolume(_imagePath);

        var partage = index.FindDirectory("Partage");
        Assert.NotNull(partage);

        var names = index.GetChildren(partage!.Value).Select(index.GetName).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { "Compta 2024", "Répertoire accentué", "Vide", "creux.vhd", "petit.txt" }, names);

        var compta = index.FindDirectory("partage\\COMPTA 2024"); // insensible à la casse, comme Windows
        Assert.NotNull(compta);
        var gros = index.GetChildren(compta!.Value).Single(c => index.GetName(c) == "gros.bin");
        Assert.Equal(200_000, index.GetSize(gros));
        Assert.False(index.IsDirectory(gros));

        var creux = index.GetChildren(partage.Value).Single(c => index.GetName(c) == "creux.vhd");
        Assert.Equal(3_000_000_000, index.GetSize(creux));

        var petit = index.GetChildren(partage.Value).Single(c => index.GetName(c) == "petit.txt");
        Assert.Equal(new DateTime(2019, 3, 4, 5, 6, 7, DateTimeKind.Utc), index.GetLastWriteUtc(petit));
    }

    [Fact]
    public void ReadVolume_HidesNtfsMetafiles()
    {
        var index = MftIndex.ReadVolume(_imagePath);
        var rootNames = index.GetChildren(MftIndex.RootRecord).Select(index.GetName).ToList();

        Assert.Contains("Partage", rootNames);
        Assert.DoesNotContain(rootNames, n => n!.StartsWith('$'));
    }

    [Fact]
    public void Scan_WithMftSource_MatchesExpectedTree()
    {
        var index = MftIndex.ReadVolume(_imagePath);
        var source = new MftScanSource(index, @"X:\Partage", "Partage");
        var result = new FileSystemScanner().Scan(new ScanOptions { RootPath = @"X:\Partage" }, source, new InMemoryAuditLogger());

        // petit.txt + lien.txt (second lien physique) + gros.bin + f1.pdf + été.docx + creux.vhd
        Assert.Equal(6, result.RootNode.FileCount);
        Assert.Equal(4, result.RootNode.FolderCount);
        Assert.Equal(3 + 3 + 200_000 + 10 + 1 + 3_000_000_000L, result.RootNode.SizeInBytes);
        Assert.Equal("lecture directe de la MFT NTFS", result.ScanMethod);

        var facture = result.RootNode.FindByRelativePath("Compta 2024/Factures/f1.pdf");
        Assert.NotNull(facture);
        Assert.Equal(Path.Join(@"X:\Partage", "Compta 2024", "Factures", "f1.pdf"), facture!.FullPath);

        Assert.Contains(result.EmptyFolders, f => f.RelativePath == "Vide");
        Assert.Null(result.RootNode.FindByRelativePath("supprime.txt"));
    }

    [Fact]
    public void ParseRunList_HandlesNegativeRelativeOffsets()
    {
        // En-tête d'attribut non résident minimal : décalage de la runlist en 0x20.
        var attribute = new byte[0x40];
        attribute[0x20] = 0x30;
        var runs = new byte[]
        {
            0x21, 0x10, 0x00, 0x01,       // 16 clusters au LCN 256
            0x11, 0x08, 0xF0,             // 8 clusters au LCN 256 - 16 = 240 (décalage négatif)
            0x00,
        };
        runs.CopyTo(attribute, 0x30);

        var parsed = MftIndex.ParseRunList(attribute);

        Assert.Equal(new[] { (256L, 16L), (240L, 8L) }, parsed);
    }

    [Fact]
    public void ApplyFixups_RejectsTornRecord()
    {
        var index = MftIndex.ReadVolume(_imagePath); // garantit que l'image est valide
        Assert.True(index.EntryCount > 0);

        var record = new byte[1024];
        "FILE"u8.CopyTo(record);
        record[0x04] = 0x30; // tableau de correction en 0x30
        record[0x06] = 3;    // numéro de contrôle + 2 secteurs
        record[0x30] = 0xAB;
        record[510] = 0xAB;
        record[1022] = 0xCD; // secteur 2 incohérent : écriture interrompue

        Assert.False(MftIndex.ApplyFixups(record));
    }
}
