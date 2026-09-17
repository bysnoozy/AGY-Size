using AgySize.Core.Logging;
using AgySize.Core.Models;
using AgySize.Core.Reporting;
using AgySize.Core.Scanning;

if (args.Length == 0 || args[0] != "scan" || args.Length < 2)
{
    Console.WriteLine("Usage : agysize scan <dossier> [--csv fichier.csv] [--html fichier.html] [--permissions]");
    return 1;
}

var rootPath = args[1];
string? csvPath = null;
string? htmlPath = null;
var analyzePermissions = false;

for (var i = 2; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--csv" when i + 1 < args.Length:
            csvPath = args[++i];
            break;
        case "--html" when i + 1 < args.Length:
            htmlPath = args[++i];
            break;
        case "--permissions":
            analyzePermissions = true;
            break;
    }
}

var logger = new InMemoryAuditLogger();
logger.EntryLogged += (_, entry) => Console.WriteLine($"[{entry.Level}] {entry.Message}");

var scanner = new FileSystemScanner();
var options = new ScanOptions { RootPath = rootPath, AnalyzePermissions = analyzePermissions };

ScanResult result;
try
{
    result = scanner.Scan(options, logger);
}
catch (DirectoryNotFoundException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

Console.WriteLine($"Fichiers : {result.RootNode.FileCount:N0}");
Console.WriteLine($"Dossiers : {result.RootNode.FolderCount:N0}");
Console.WriteLine($"Taille totale : {result.RootNode.SizeInBytes:N0} octets");

if (csvPath is not null)
{
    CsvReportWriter.WriteSizeReport(result, csvPath);
    Console.WriteLine($"Export CSV : {csvPath}");
}

if (htmlPath is not null)
{
    HtmlReportWriter.WriteReport(result, htmlPath);
    Console.WriteLine($"Export HTML : {htmlPath}");
}

return 0;
