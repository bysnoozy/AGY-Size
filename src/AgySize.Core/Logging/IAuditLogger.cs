using AgySize.Core.Models;

namespace AgySize.Core.Logging;

public interface IAuditLogger
{
    void Log(LogLevel level, string source, string message);
}

public static class AuditLoggerExtensions
{
    public static void Debug(this IAuditLogger logger, string source, string message) =>
        logger.Log(LogLevel.Debug, source, message);

    public static void Info(this IAuditLogger logger, string source, string message) =>
        logger.Log(LogLevel.Info, source, message);

    public static void Warning(this IAuditLogger logger, string source, string message) =>
        logger.Log(LogLevel.Warning, source, message);

    public static void Error(this IAuditLogger logger, string source, string message) =>
        logger.Log(LogLevel.Error, source, message);
}
