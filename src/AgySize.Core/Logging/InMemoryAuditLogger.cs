using AgySize.Core.Models;

namespace AgySize.Core.Logging;

/// <summary>
/// Journal en mémoire, borné en nombre d'entrées, qui notifie les abonnés (ex. le panneau "Journal"
/// de l'application) à chaque nouvelle entrée pour un affichage en direct.
/// </summary>
public sealed class InMemoryAuditLogger : IAuditLogger
{
    private readonly object _lock = new();
    private readonly List<LogEntry> _entries = new();
    private readonly int _capacity;

    public InMemoryAuditLogger(int capacity = 5000)
    {
        _capacity = capacity;
    }

    public event EventHandler<LogEntry>? EntryLogged;

    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (_lock)
        {
            return _entries.ToList();
        }
    }

    public void Log(LogLevel level, string source, string message)
    {
        var entry = new LogEntry
        {
            TimestampUtc = DateTime.UtcNow,
            Level = level,
            Source = source,
            Message = message,
        };

        lock (_lock)
        {
            _entries.Add(entry);
            if (_entries.Count > _capacity)
            {
                _entries.RemoveAt(0);
            }
        }

        EntryLogged?.Invoke(this, entry);
    }
}
