using AgySize.Core.Models;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace AgySize.Core.Reporting;

/// <summary>
/// Produces a clean, client-facing PDF summary of a SharePoint migration audit: key volumetry
/// figures, a short synthesis, and one section per anomaly category with the recommended action
/// followed by the full, itemized list of every affected file/folder in that category (sorted, one
/// per line). The full list can span as many pages as needed.
/// </summary>
public static class PdfReportWriter
{
    public static void WriteClientReport(ScanResult result, string filePath)
    {
        EnsureFontResolver();

        var document = new PdfDocument();
        document.Info.Title = "Rapport d'audit de migration SharePoint";
        document.Info.Author = "AGY-Size";

        var b = new PdfLayoutBuilder(document);

        var titleFont = new XFont(ClientReportFontResolver.Family, 20, XFontStyleEx.Bold);
        var subtitleFont = new XFont(ClientReportFontResolver.Family, 10);
        var sectionFont = new XFont(ClientReportFontResolver.Family, 13, XFontStyleEx.Bold);
        var categoryTitleFont = new XFont(ClientReportFontResolver.Family, 11.5, XFontStyleEx.Bold);
        var bodyFont = new XFont(ClientReportFontResolver.Family, 10);
        var listFont = new XFont(ClientReportFontResolver.Family, 9);
        var kpiValueFont = new XFont(ClientReportFontResolver.Family, 17, XFontStyleEx.Bold);
        var kpiLabelFont = new XFont(ClientReportFontResolver.Family, 8.5);

        var gray = new XSolidBrush(XColor.FromArgb(90, 90, 90));
        var blockingColor = new XSolidBrush(XColor.FromArgb(168, 0, 0));
        var warningColor = new XSolidBrush(XColor.FromArgb(138, 109, 0));

        b.DrawLine("Rapport d'audit de migration SharePoint", titleFont, XBrushes.Black, 28);
        b.DrawLine($"Dossier analysé : {result.RootPath}", subtitleFont, gray, 15);
        b.DrawLine($"Rapport généré le {result.ScanCompletedUtc:dd/MM/yyyy} à {result.ScanCompletedUtc:HH:mm}", subtitleFont, gray, 15);
        b.AddSpace(16);

        var categories = result.AuditIssues
            .GroupBy(i => i.Type)
            .Select(g => new CategorySummary(
                g.Key,
                g.Count(),
                g.Max(i => i.Severity),
                g.Select(i => i.RelativePath).Distinct().OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderByDescending(c => c.Severity)
            .ThenByDescending(c => c.Count)
            .ToList();

        var blockingCount = result.AuditIssues.Count(i => i.Severity == AuditSeverity.Blocking);
        var warningCount = result.AuditIssues.Count(i => i.Severity == AuditSeverity.Warning);

        DrawKpiRow(
            b,
            new[]
            {
                ("Fichiers analysés", result.RootNode.FileCount.ToString("N0")),
                ("Dossiers analysés", result.RootNode.FolderCount.ToString("N0")),
                ("Taille totale", FormatBytes(result.RootNode.SizeInBytes)),
                ("Anomalies bloquantes", blockingCount.ToString("N0")),
                ("Avertissements", warningCount.ToString("N0")),
            },
            kpiValueFont,
            kpiLabelFont);
        b.AddSpace(22);

        b.DrawLine("Synthèse", sectionFont, XBrushes.Black, 20);
        b.AddSpace(4);

        var synthese = categories.Count == 0
            ? "Aucune anomalie n'a été détectée sur l'arborescence analysée au regard des contraintes de migration SharePoint retenues pour cet audit."
            : $"{result.AuditIssues.Count:N0} anomalie(s) ont été détectées, réparties en {categories.Count} catégorie(s), " +
              $"dont {blockingCount:N0} bloquante(s) pour la migration et {warningCount:N0} à vérifier. " +
              "Le détail par catégorie et les actions à réaliser avant la migration sont présentés ci-dessous.";

        b.EnsureSpace(b.MeasureWrappedHeight(synthese, bodyFont) + 20);
        b.DrawWrapped(synthese, bodyFont, XBrushes.Black);
        b.AddSpace(24);

        if (categories.Count > 0)
        {
            b.DrawLine("Détail par catégorie et actions à réaliser", sectionFont, XBrushes.Black, 20);
            b.AddSpace(6);

            foreach (var category in categories)
            {
                DrawCategorySection(b, category, categoryTitleFont, bodyFont, listFont, blockingColor, warningColor, gray);
            }
        }

        document.Save(filePath);
    }

    private static void DrawKpiRow(PdfLayoutBuilder b, (string Label, string Value)[] kpis, XFont valueFont, XFont labelFont)
    {
        const double cardHeight = 46;
        var gfx = b.Graphics;
        var cardWidth = b.ContentWidth / kpis.Length;
        var startY = b.CurrentY;

        for (var i = 0; i < kpis.Length; i++)
        {
            var x = b.LeftMargin + i * cardWidth;
            var rect = new XRect(x, startY, cardWidth - 6, cardHeight);
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(243, 242, 241)), rect);
            gfx.DrawString(kpis[i].Value, valueFont, XBrushes.Black, new XRect(x + 8, startY + 6, cardWidth - 16, 22), XStringFormats.TopLeft);
            gfx.DrawString(kpis[i].Label, labelFont, new XSolidBrush(XColor.FromArgb(90, 90, 90)), new XRect(x + 8, startY + 28, cardWidth - 16, 16), XStringFormats.TopLeft);
        }

        b.AddSpace(cardHeight);
    }

    private static void DrawCategorySection(
        PdfLayoutBuilder b,
        CategorySummary category,
        XFont titleFont,
        XFont bodyFont,
        XFont listFont,
        XBrush blockingColor,
        XBrush warningColor,
        XBrush grayBrush)
    {
        var (label, action) = RemediationAdvice.Get(category.Type);
        var isBlocking = category.Severity == AuditSeverity.Blocking;
        var severityColor = isBlocking ? blockingColor : warningColor;
        var countText = category.Count == 1 ? "1 élément concerné" : $"{category.Count:N0} éléments concernés";
        var actionText = "Action recommandée : " + action;

        const double titleLineHeight = 18;
        var actionHeight = b.MeasureWrappedHeight(actionText, bodyFont);

        // The header and action text must stay together; the itemized list below is drawn one line
        // at a time and is free to flow across a page break on its own.
        b.EnsureSpace(titleLineHeight + actionHeight + 18);

        b.DrawMarker(severityColor, titleLineHeight);
        b.DrawLabelValueLine("     " + label, countText, titleFont, XBrushes.Black, titleLineHeight);
        b.DrawWrapped(actionText, bodyFont, XBrushes.Black);
        b.AddSpace(6);

        var listLineHeight = listFont.GetHeight() + 3;
        foreach (var path in category.AllRelativePaths)
        {
            b.EnsureSpace(listLineHeight);
            b.DrawListItem(ShortenPath(path, 100), listFont, grayBrush, listLineHeight);
        }

        b.AddSpace(8);
        b.EnsureSpace(2);
        b.DrawSeparator();
        b.AddSpace(10);
    }

    private static void EnsureFontResolver()
    {
        GlobalFontSettings.FontResolver ??= new ClientReportFontResolver();
    }

    /// <summary>
    /// Shortens a relative path for display in the client report, keeping the start of the path and
    /// the file name (the most useful parts) and collapsing the middle. Deeply nested folders can
    /// otherwise produce a single unreadable line of dozens of path segments.
    /// </summary>
    private static string ShortenPath(string relativePath, int maxLength = 60)
    {
        if (relativePath.Length <= maxLength)
        {
            return relativePath;
        }

        var lastSlash = relativePath.LastIndexOf('/');
        var fileName = lastSlash >= 0 ? relativePath[(lastSlash + 1)..] : relativePath;

        // Not enough room to show any path prefix alongside the file name: show only its tail.
        if (fileName.Length >= maxLength - 1)
        {
            var tailLength = Math.Min(Math.Max(1, maxLength - 1), fileName.Length);
            return "…" + fileName[^tailLength..];
        }

        var prefixBudget = Math.Clamp(maxLength - fileName.Length - 2, 0, relativePath.Length);
        var prefix = relativePath[..prefixBudget];
        return $"{prefix}…/{fileName}";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "o", "Ko", "Mo", "Go", "To" };
        double size = bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:F2} {units[unitIndex]}";
    }

    private sealed record CategorySummary(
        AuditIssueType Type,
        int Count,
        AuditSeverity Severity,
        IReadOnlyList<string> AllRelativePaths);
}
