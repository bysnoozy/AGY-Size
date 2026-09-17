using System.Net;
using System.Text;
using AgySize.Core.Models;

namespace AgySize.Core.Reporting;

public static class HtmlReportWriter
{
    public static void WriteReport(ScanResult result, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"fr\"><head><meta charset=\"utf-8\">");
        sb.AppendLine("<title>Rapport AGY-Size</title>");
        sb.AppendLine("<style>" + Css + "</style></head><body>");
        sb.AppendLine($"<h1>Rapport AGY-Size — {Encode(result.RootPath)}</h1>");
        sb.AppendLine($"<p>Analyse du {result.ScanStartedUtc:yyyy-MM-dd HH:mm} UTC au {result.ScanCompletedUtc:yyyy-MM-dd HH:mm} UTC.</p>");

        sb.AppendLine("<h2>Volumétrie</h2><ul>");
        sb.AppendLine($"<li>Fichiers : {result.RootNode.FileCount:N0}</li>");
        sb.AppendLine($"<li>Dossiers : {result.RootNode.FolderCount:N0}</li>");
        sb.AppendLine($"<li>Taille totale : {FormatBytes(result.RootNode.SizeInBytes)}</li>");
        sb.AppendLine($"<li>Dossiers vides : {result.EmptyFolders.Count:N0}</li>");
        sb.AppendLine($"<li>Erreurs de lecture : {result.Errors.Count:N0}</li>");
        sb.AppendLine("</ul>");

        AppendTable(
            sb,
            "Dossiers les plus volumineux",
            new[] { "Chemin", "Taille" },
            result.LargestFolders.Select(f => new[] { f.RelativePath, FormatBytes(f.SizeInBytes) }));

        AppendTable(
            sb,
            "Fichiers les plus volumineux",
            new[] { "Chemin", "Taille" },
            result.LargestFiles.Select(f => new[] { f.RelativePath, FormatBytes(f.SizeInBytes) }));

        AppendTable(
            sb,
            "Répartition par type",
            new[] { "Extension", "Fichiers", "Taille" },
            result.ExtensionStats.Select(e => new[] { e.Extension, e.FileCount.ToString("N0"), FormatBytes(e.TotalSizeInBytes) }));

        if (result.PermissionFindings.Count > 0)
        {
            AppendTable(
                sb,
                "Constats sur les droits d'accès",
                new[] { "Sévérité", "Catégorie", "Chemin", "Identité", "Droits", "Description" },
                result.PermissionFindings.Select(p => new[]
                {
                    p.Severity.ToString(), p.Category.ToString(), p.RelativePath,
                    p.IdentityName ?? string.Empty, p.Rights ?? string.Empty, p.Description,
                }));
        }

        if (result.Errors.Count > 0)
        {
            sb.AppendLine("<h2>Erreurs de lecture</h2><ul>");
            foreach (var error in result.Errors)
            {
                sb.AppendLine($"<li>{Encode(error)}</li>");
            }

            sb.AppendLine("</ul>");
        }

        sb.AppendLine("</body></html>");
        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    private static void AppendTable(StringBuilder sb, string title, string[] headers, IEnumerable<string[]> rows)
    {
        sb.AppendLine($"<h2>{Encode(title)}</h2><table><thead><tr>");
        foreach (var header in headers)
        {
            sb.AppendLine($"<th>{Encode(header)}</th>");
        }

        sb.AppendLine("</tr></thead><tbody>");
        foreach (var row in rows)
        {
            sb.AppendLine("<tr>" + string.Concat(row.Select(cell => $"<td>{Encode(cell)}</td>")) + "</tr>");
        }

        sb.AppendLine("</tbody></table>");
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

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

    private const string Css = @"
        body { font-family: 'Segoe UI', Arial, sans-serif; margin: 24px; color: #1f2933; background:#f5f7fa; }
        h1 { color: #1b3a63; }
        h2 { color: #33475b; border-bottom: 2px solid #4f7cac; padding-bottom: 4px; }
        table { border-collapse: collapse; width: 100%; margin-bottom: 24px; background: white; }
        th, td { border: 1px solid #d3dce6; padding: 6px 10px; text-align: left; font-size: 13px; }
        th { background: #33475b; color: white; }
        tr:nth-child(even) { background: #eef2f7; }
    ";
}
