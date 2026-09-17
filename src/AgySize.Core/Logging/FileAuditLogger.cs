using AgySize.Core.Models;

namespace AgySize.Core.Logging;

/// <summary>
/// Journal technique persistant sur disque : chaque exécution ajoute ses lignes au fichier existant
/// (append), pour conserver un historique des scans, erreurs de lecture et actions effectuées.
/// </summary>
public sealed class FileAuditLogger : IAuditLogger, IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _lock = new();

    public FileAuditLogger(string filePath)
    {
        FilePath = filePath;
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(filePath, append: true) { AutoFlush = true };
    }

    public string FilePath { get; }

    public void Log(LogLevel level, string source, string message)
    {
        lock (_lock)
        {
            _writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level,-7}] {source} - {message}");
        }
    }

    public void Dispose() => _writer.Dispose();
}
