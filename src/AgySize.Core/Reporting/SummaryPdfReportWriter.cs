using AgySize.Core.Models;
using AgySize.Core.Scanning;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace AgySize.Core.Reporting;

/// <summary>
/// Rapport PDF de synthèse d'une analyse (remplace l'ancien rapport HTML, qui listait chaque
/// anomalie et chaque constat de droits et pouvait dépasser des milliers de pages sur un serveur).
/// Ne contient que des chiffres clés et des "top N" : il tient en une à deux pages quelle que soit
/// la taille de l'arborescence. Le détail exhaustif reste disponible via les exports CSV.
/// </summary>
public static class SummaryPdfReportWriter
{
    private const int TopCount = 10;

    public static void WriteReport(ScanResult result, string filePath, int oldFileThresholdDays = 365)
    {
        GlobalFontSettings.FontResolver ??= new ClientReportFontResolver();

        var document = new PdfDocument();
        document.Info.Title = "Rapport de synthèse AGY-Size";
        document.Info.Author = "AGY-Size";

        var b = new PdfLayoutBuilder(document);
        var f = new Fonts();
        var gray = new XSolidBrush(XColor.FromArgb(90, 90, 90));
        var total = Math.Max(1, result.RootNode.SizeInBytes);

        b.DrawLine("Rapport de synthèse AGY-Size", f.Title, XBrushes.Black, 26);
        b.DrawLine($"Dossier analysé : {result.RootPath}", f.Small, gray, 13);
        var local = result.ScanCompletedUtc.ToLocalTime();
        b.DrawLine($"Analyse du {local:dd/MM/yyyy} à {local:HH:mm} — AGY-Size {AppVersion.Display}", f.Small, gray, 13);
        b.AddSpace(12);

        // Fichiers anciens : nombre et volume, sans les lister.
        var threshold = DateTime.UtcNow.AddDays(-oldFileThresholdDays);
        long oldCount = 0, oldSize = 0;
        foreach (var file in result.RootNode.Files())
        {
            if (file.LastWriteUtc < threshold)
            {
                oldCount++;
                oldSize += file.SizeInBytes;
            }
        }

        DrawKpiRow(b, f, new[]
        {
            ("Taille totale", FormatBytes(result.RootNode.SizeInBytes)),
            ("Fichiers", result.RootNode.FileCount.ToString("N0")),
            ("Dossiers", result.RootNode.FolderCount.ToString("N0")),
            ("Dossiers vides", result.EmptyFolders.Count.ToString("N0")),
            ($"Non modifiés > {oldFileThresholdDays / 365} an(s)", $"{FormatBytes(oldSize)}"),
        });
        b.AddSpace(6);
        b.DrawLine(
            $"{oldCount:N0} fichier(s) non modifié(s) depuis plus de {oldFileThresholdDays} jours " +
            $"({oldSize * 100.0 / total:N1} % du volume) — {result.Errors.Count:N0} erreur(s) de lecture.",
            f.Small, gray, 13);
        b.AddSpace(10);

        // Dossiers de premier niveau : plus parlant qu'un "top" mêlant un dossier et ses propres sous-dossiers.
        Section(b, f, "Dossiers de premier niveau les plus volumineux");
        DrawTable(b, f, new[] { ("Dossier", 0.70), ("Taille", 0.18), ("%", 0.12) },
            result.RootNode.Children
                .Where(n => n.Kind == FileSystemNodeKind.Folder)
                .OrderByDescending(n => n.SizeInBytes)
                .Take(TopCount)
                .Select(n => new[] { n.RelativePath, FormatBytes(n.SizeInBytes), Percent(n.SizeInBytes, total) }));

        Section(b, f, "Fichiers les plus volumineux");
        DrawTable(b, f, new[] { ("Fichier", 0.70), ("Taille", 0.18), ("%", 0.12) },
            result.LargestFiles.Take(TopCount)
                .Select(n => new[] { n.RelativePath, FormatBytes(n.SizeInBytes), Percent(n.SizeInBytes, total) }));

        Section(b, f, "Répartition par type de fichier");
        var extensions = result.ExtensionStats.Take(TopCount).ToList();
        var others = result.ExtensionStats.Skip(TopCount).ToList();
        var extensionRows = extensions
            .Select(e => new[] { e.Extension, e.FileCount.ToString("N0"), FormatBytes(e.TotalSizeInBytes), Percent(e.TotalSizeInBytes, total) })
            .ToList();
        if (others.Count > 0)
        {
            var otherSize = others.Sum(e => e.TotalSizeInBytes);
            extensionRows.Add(new[] { $"Autres ({others.Count} types)", others.Sum(e => e.FileCount).ToString("N0"), FormatBytes(otherSize), Percent(otherSize, total) });
        }

        DrawTable(b, f, new[] { ("Extension", 0.46), ("Fichiers", 0.18), ("Taille", 0.22), ("%", 0.14) }, extensionRows);

        Section(b, f, "Conformité SharePoint Online / OneDrive");
        if (result.AuditIssues.Count == 0)
        {
            b.DrawLine("Aucune anomalie détectée.", f.Body, XBrushes.Black, 16);
        }
        else
        {
            var blocking = result.AuditIssues.Count(i => i.Severity == AuditSeverity.Blocking);
            b.DrawLine($"{result.AuditIssues.Count:N0} anomalie(s), dont {blocking:N0} bloquante(s) pour la migration.", f.Body, XBrushes.Black, 16);
            DrawTable(b, f, new[] { ("Catégorie", 0.62), ("Gravité", 0.20), ("Éléments", 0.18) },
                result.AuditIssues
                    .GroupBy(i => i.Type)
                    .Select(g => (Type: g.Key, Count: g.Count(), Severity: g.Max(i => i.Severity)))
                    .OrderByDescending(c => c.Severity).ThenByDescending(c => c.Count)
                    .Select(c => new[]
                    {
                        RemediationAdvice.Get(c.Type).Label,
                        c.Severity == AuditSeverity.Blocking ? "Bloquant" : "À vérifier",
                        c.Count.ToString("N0"),
                    }));
        }

        var permissionIssues = result.PermissionFindings.Where(p => p.Category != PermissionFindingCategory.OwnerInfo).ToList();
        if (result.PermissionFindings.Count > 0)
        {
            Section(b, f, "Droits d'accès NTFS");
            if (permissionIssues.Count == 0)
            {
                b.DrawLine("Aucun constat particulier sur les dossiers analysés.", f.Body, XBrushes.Black, 16);
            }
            else
            {
                DrawTable(b, f, new[] { ("Constat", 0.62), ("Gravité", 0.20), ("Dossiers", 0.18) },
                    permissionIssues
                        .GroupBy(p => p.Category)
                        .Select(g => (Category: g.Key, Count: g.Select(p => p.RelativePath).Distinct().Count(), Severity: g.Max(p => p.Severity)))
                        .OrderByDescending(c => c.Severity).ThenByDescending(c => c.Count)
                        .Select(c => new[] { PermissionLabel(c.Category), SeverityLabel(c.Severity), c.Count.ToString("N0") }));
            }
        }

        b.AddSpace(8);
        b.EnsureSpace(30);
        b.DrawWrapped(
            "Rapport volontairement synthétique. Le détail complet (chaque fichier, chaque anomalie) est " +
            "disponible dans AGY-Size et via les exports CSV ; le rapport PDF client de la section " +
            "« Mise en conformité SharePoint » détaille les actions à mener par catégorie.",
            f.Small, gray);

        document.Save(filePath);
    }

    internal static void Section(PdfLayoutBuilder b, Fonts f, string title)
    {
        b.AddSpace(8);
        b.EnsureSpace(60);
        b.DrawLine(title, f.Section, new XSolidBrush(XColor.FromArgb(27, 58, 99)), 18);
        b.AddSpace(2);
    }

    internal static void DrawKpiRow(PdfLayoutBuilder b, Fonts f, (string Label, string Value)[] kpis)
    {
        const double cardHeight = 44;
        var gfx = b.Graphics;
        var cardWidth = b.ContentWidth / kpis.Length;
        var y = b.CurrentY;
        var background = new XSolidBrush(XColor.FromArgb(238, 242, 247));
        var gray = new XSolidBrush(XColor.FromArgb(90, 90, 90));

        for (var i = 0; i < kpis.Length; i++)
        {
            var x = b.LeftMargin + i * cardWidth;
            gfx.DrawRectangle(background, new XRect(x, y, cardWidth - 6, cardHeight));
            gfx.DrawString(Fit(gfx, kpis[i].Value, f.Kpi, cardWidth - 14), f.Kpi, XBrushes.Black, new XRect(x + 7, y + 6, cardWidth - 14, 20), XStringFormats.TopLeft);
            gfx.DrawString(Fit(gfx, kpis[i].Label, f.KpiLabel, cardWidth - 14), f.KpiLabel, gray, new XRect(x + 7, y + 27, cardWidth - 14, 14), XStringFormats.TopLeft);
        }

        b.AddSpace(cardHeight);
    }

    /// <summary>Tableau simple : une ligne par entrée, textes tronqués pour tenir dans leur colonne (pas de retour à la ligne).</summary>
    internal static void DrawTable(PdfLayoutBuilder b, Fonts f, (string Header, double Width)[] columns, IEnumerable<string[]> rows, int leftColumns = 1)
    {
        const double rowHeight = 15;
        var headerBrush = new XSolidBrush(XColor.FromArgb(51, 71, 91));
        var stripeBrush = new XSolidBrush(XColor.FromArgb(244, 246, 249));

        void DrawRow(string[] cells, XFont font, XBrush text, XBrush? background)
        {
            b.EnsureSpace(rowHeight);
            var g = b.Graphics;
            var y = b.CurrentY;
            if (background is not null)
            {
                g.DrawRectangle(background, new XRect(b.LeftMargin, y, b.ContentWidth, rowHeight));
            }

            var x = b.LeftMargin;
            for (var i = 0; i < columns.Length; i++)
            {
                var width = columns[i].Width * b.ContentWidth;
                var alignRight = i >= leftColumns;
                var cell = Fit(g, i < cells.Length ? cells[i] : string.Empty, font, width - 8, keepEnd: i < leftColumns);
                g.DrawString(cell, font, text, new XRect(x + 4, y + 2, width - 8, rowHeight - 2),
                    alignRight ? XStringFormats.TopRight : XStringFormats.TopLeft);
                x += width;
            }

            b.AddSpace(rowHeight);
        }

        DrawRow(columns.Select(c => c.Header).ToArray(), f.TableHeader, XBrushes.White, headerBrush);
        var index = 0;
        foreach (var row in rows)
        {
            DrawRow(row, f.Table, XBrushes.Black, index++ % 2 == 1 ? stripeBrush : null);
        }

        if (index == 0)
        {
            DrawRow(new[] { "(aucun)" }, f.Table, XBrushes.Gray, null);
        }
    }

    /// <summary>Tronque un texte avec "…" pour qu'il tienne dans la largeur donnée ; pour un chemin, garde plutôt la fin (le nom).</summary>
    internal static string Fit(XGraphics gfx, string text, XFont font, double width, bool keepEnd = false)
    {
        if (gfx.MeasureString(text, font).Width <= width)
        {
            return text;
        }

        int low = 0, high = text.Length;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            var candidate = keepEnd ? "…" + text[^mid..] : text[..mid] + "…";
            if (gfx.MeasureString(candidate, font).Width <= width)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return keepEnd ? "…" + text[^low..] : text[..low] + "…";
    }

    private static string PermissionLabel(PermissionFindingCategory category) => category switch
    {
        PermissionFindingCategory.BrokenInheritance => "Héritage des autorisations désactivé",
        PermissionFindingCategory.BroadGrant => "Droits étendus pour une identité générique (Tout le monde...)",
        PermissionFindingCategory.ExplicitDeny => "Refus explicites",
        PermissionFindingCategory.AnalysisError => "Autorisations illisibles",
        _ => category.ToString(),
    };

    private static string SeverityLabel(PermissionSeverity severity) => severity switch
    {
        PermissionSeverity.Critical => "Critique",
        PermissionSeverity.Warning => "À vérifier",
        _ => "Info",
    };

    internal static string Percent(long part, long total) => $"{part * 100.0 / total:N1} %";

    internal static string FormatBytes(long bytes)
    {
        string[] units = { "o", "Ko", "Mo", "Go", "To" };
        double size = bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:N2} {units[unitIndex]}";
    }

    internal sealed class Fonts
    {
        public XFont Title { get; } = new(ClientReportFontResolver.Family, 18, XFontStyleEx.Bold);
        public XFont Section { get; } = new(ClientReportFontResolver.Family, 12, XFontStyleEx.Bold);
        public XFont Body { get; } = new(ClientReportFontResolver.Family, 10);
        public XFont Small { get; } = new(ClientReportFontResolver.Family, 8.5);
        public XFont Table { get; } = new(ClientReportFontResolver.Family, 8.5);
        public XFont TableHeader { get; } = new(ClientReportFontResolver.Family, 8.5, XFontStyleEx.Bold);
        public XFont Kpi { get; } = new(ClientReportFontResolver.Family, 14, XFontStyleEx.Bold);
        public XFont KpiLabel { get; } = new(ClientReportFontResolver.Family, 7.5);
    }
}
