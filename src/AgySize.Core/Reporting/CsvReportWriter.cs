using System.Text;
using AgySize.Core.Models;
using AgySize.Core.Scanning;

namespace AgySize.Core.Reporting;

public static class CsvReportWriter
{
    public static void WriteSizeReport(ScanResult result, string filePath)
    {
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
        writer.WriteLine("Type;Chemin;Taille (octets);Dernière modification");

        foreach (var node in result.RootNode.Flatten().OrderByDescending(n => n.SizeInBytes))
        {
            var kind = node.Kind == FileSystemNodeKind.Folder ? "Dossier" : "Fichier";
            var path = node.RelativePath.Length == 0 ? "(racine)" : node.RelativePath;
            writer.WriteLine($"{kind};{EscapeCsv(path)};{node.SizeInBytes};{node.LastWriteUtc:yyyy-MM-dd HH:mm:ss}");
        }
    }

    public static void WritePermissionFindings(ScanResult result, string filePath)
    {
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
        writer.WriteLine("Sévérité;Catégorie;Chemin;Identité;Droits;Description");

        foreach (var finding in result.PermissionFindings)
        {
            writer.WriteLine(
                $"{finding.Severity};{finding.Category};{EscapeCsv(finding.RelativePath)};" +
                $"{EscapeCsv(finding.IdentityName ?? string.Empty)};{EscapeCsv(finding.Rights ?? string.Empty)};" +
                $"{EscapeCsv(finding.Description)}");
        }
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
