using AgySize.Core.Models;
using AgySize.Core.Scanning;
using AgySize.Core.Sharing;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using static AgySize.Core.Reporting.SummaryPdfReportWriter;

namespace AgySize.Core.Reporting;

/// <summary>
/// Rapport PDF client "Partages réseau" : ne présente que ce qui est réellement partagé sur le
/// serveur (chemin \\SERVEUR\Partage et dossier local correspondant), avec la volumétrie de chaque
/// partage, ses principaux sous-dossiers, l'ancienneté des données et les anomalies SharePoint.
/// Synthétique (quelques lignes par partage), pour donner au client une vision d'ensemble.
/// </summary>
public static class SharesPdfReportWriter
{
    private const int SubfolderCount = 5;

    public static void WriteReport(ScanResult result, IReadOnlyList<ShareCoverage> shares, string filePath, int oldFileThresholdDays = 365)
    {
        GlobalFontSettings.FontResolver ??= new ClientReportFontResolver();

        var document = new PdfDocument();
        document.Info.Title = "Rapport des partages réseau";
        document.Info.Author = "AGY-Size";

        var b = new PdfLayoutBuilder(document);
        var f = new Fonts();
        var gray = new XSolidBrush(XColor.FromArgb(90, 90, 90));
        var blue = new XSolidBrush(XColor.FromArgb(27, 58, 99));
        var threshold = DateTime.UtcNow.AddDays(-oldFileThresholdDays);

        var covered = shares.Where(s => s.IsCovered).ToList();
        var topLevel = ShareMatcher.TopLevel(covered);
        var stats = covered.ToDictionary(c => c, c => Compute(result, c.Node!, threshold));

        b.DrawLine("Rapport des partages réseau", f.Title, XBrushes.Black, 26);
        var server = covered.Select(c => c.Share.UncPath.TrimStart('\\').Split('\\')[0]).FirstOrDefault() ?? Environment.MachineName;
        b.DrawLine($"Serveur : {server}", f.Body, XBrushes.Black, 15);
        var local = result.ScanCompletedUtc.ToLocalTime();
        b.DrawWrapped($"Analyse du {local:dd/MM/yyyy} à {local:HH:mm} — périmètre analysé : {result.RootPath}", f.Small, gray);
        b.AddSpace(12);

        long totalSize = topLevel.Sum(c => c.Node!.SizeInBytes);
        long totalFiles = topLevel.Sum(c => (long)c.Node!.FileCount);
        long totalOld = topLevel.Sum(c => stats[c].OldSize);

        DrawKpiRow(b, f, new[]
        {
            ("Partages", covered.Count.ToString("N0")),
            ("Volume partagé", FormatBytes(totalSize)),
            ("Fichiers partagés", totalFiles.ToString("N0")),
            ($"Non modifié > {oldFileThresholdDays / 365} an(s)", totalSize == 0 ? "-" : Percent(totalOld, totalSize)),
        });
        b.AddSpace(10);

        if (covered.Count == 0)
        {
            b.DrawWrapped(
                "Aucun partage réseau n'a été trouvé dans le périmètre analysé. Pour couvrir tous les partages, " +
                "lancez l'analyse sur le serveur lui-même, à la racine du ou des volumes qui les hébergent (ex. D:\\).",
                f.Body, XBrushes.Black);
        }
        else
        {
            Section(b, f, "Vue d'ensemble des partages");
            var totalForPercent = Math.Max(1, totalSize);
            DrawTable(b, f,
                new[] { ("Partage", 0.30), ("Chemin local", 0.28), ("Taille", 0.13), ("% vol.", 0.09), ("Fichiers", 0.11), ("Ancien", 0.09) },
                covered
                    .OrderByDescending(c => c.Node!.SizeInBytes)
                    .Select(c => new[]
                    {
                        c.Share.UncPath,
                        c.Share.LocalPath,
                        FormatBytes(c.Node!.SizeInBytes),
                        Percent(c.Node.SizeInBytes, totalForPercent),
                        c.Node.FileCount.ToString("N0"),
                        c.Node.SizeInBytes == 0 ? "-" : Percent(stats[c].OldSize, c.Node.SizeInBytes),
                    }),
                leftColumns: 2);
            b.DrawLine($"« Ancien » : part du volume non modifiée depuis plus de {oldFileThresholdDays} jours.", f.Small, gray, 13);

            Section(b, f, "Détail par partage");
            foreach (var share in covered.OrderBy(c => c.Share.Name, StringComparer.OrdinalIgnoreCase))
            {
                var s = stats[share];
                var node = share.Node!;
                b.EnsureSpace(40 + 15 * (SubfolderCount + 1));
                b.DrawLine(share.Share.UncPath, f.Section, blue, 17);
                var description = share.Share.Remark is null ? string.Empty : $" — {share.Share.Remark}";
                b.DrawWrapped($"Dossier local : {share.Share.LocalPath}{description}", f.Small, gray);
                b.DrawWrapped(
                    $"{FormatBytes(node.SizeInBytes)} · {node.FileCount:N0} fichier(s) · {node.FolderCount:N0} dossier(s) · " +
                    $"dernière modification : {(s.Newest is { } n ? n.ToLocalTime().ToString("dd/MM/yyyy") : "-")} · " +
                    $"anomalies SharePoint : {s.Issues:N0} (dont {s.BlockingIssues:N0} bloquante(s))",
                    f.Small, XBrushes.Black);
                b.AddSpace(4);

                var subfolders = node.Children
                    .Where(c => c.Kind == FileSystemNodeKind.Folder)
                    .OrderByDescending(c => c.SizeInBytes)
                    .ToList();
                var rows = subfolders.Take(SubfolderCount)
                    .Select(c => new[] { $@"{share.Share.UncPath}\{c.Name}", FormatBytes(c.SizeInBytes), Percent(c.SizeInBytes, Math.Max(1, node.SizeInBytes)) })
                    .ToList();
                if (subfolders.Count > SubfolderCount)
                {
                    var rest = subfolders.Skip(SubfolderCount).ToList();
                    var restSize = rest.Sum(c => c.SizeInBytes);
                    rows.Add(new[] { $"Autres sous-dossiers ({rest.Count})", FormatBytes(restSize), Percent(restSize, Math.Max(1, node.SizeInBytes)) });
                }

                if (rows.Count > 0)
                {
                    DrawTable(b, f, new[] { ("Principaux sous-dossiers", 0.70), ("Taille", 0.18), ("%", 0.12) }, rows);
                }

                b.AddSpace(10);
            }
        }

        var uncovered = shares.Where(s => !s.IsCovered).ToList();
        if (uncovered.Count > 0)
        {
            Section(b, f, "Partages hors du périmètre analysé");
            DrawTable(b, f, new[] { ("Partage", 0.5), ("Chemin local", 0.5) },
                uncovered.Select(s => new[] { s.Share.UncPath, s.Share.LocalPath }), leftColumns: 2);
            b.DrawLine("Ces partages existent sur le serveur mais ne sont pas inclus dans le dossier analysé.", f.Small, gray, 13);
        }

        document.Save(filePath);
    }

    private sealed record ShareStats(long OldSize, DateTime? Newest, int Issues, int BlockingIssues);

    private static ShareStats Compute(ScanResult result, FileSystemNode node, DateTime threshold)
    {
        long oldSize = 0;
        DateTime? newest = null;
        foreach (var file in node.Files())
        {
            if (file.LastWriteUtc < threshold)
            {
                oldSize += file.SizeInBytes;
            }

            if (newest is null || file.LastWriteUtc > newest)
            {
                newest = file.LastWriteUtc;
            }
        }

        var prefix = node.RelativePath.Length == 0 ? string.Empty : node.RelativePath + "/";
        var issues = result.AuditIssues
            .Where(i => prefix.Length == 0 || i.RelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new ShareStats(oldSize, newest, issues.Count, issues.Count(i => i.Severity == AuditSeverity.Blocking));
    }
}
