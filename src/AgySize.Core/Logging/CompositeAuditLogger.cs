using AgySize.Core.Models;

namespace AgySize.Core.Logging;

public sealed class CompositeAuditLogger : IAuditLogger
{
    private readonly IReadOnlyList<IAuditLogger> _loggers;

    public CompositeAuditLogger(params IAuditLogger[] loggers)
    {
        _loggers = loggers;
    }

    public void Log(LogLevel level, string source, string message)
    {
        foreach (var logger in _loggers)
        {
            logger.Log(level, source, message);
        }
    }
}
