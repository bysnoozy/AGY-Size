using System.Diagnostics;
using System.Runtime.Versioning;

namespace AgySize.Core.Scheduling;

/// <summary>
/// Enregistre/supprime des scans planifiés via le Planificateur de tâches Windows (schtasks.exe),
/// pour exécuter <c>AgySize.Cli</c> périodiquement sans dépendre d'un service ou agent en tâche de
/// fond (voir la limite connue dans le README au sujet d'un futur agent).
/// </summary>
[SupportedOSPlatform("windows")]
public static class ScheduledScanManager
{
    public static void CreateDailyTask(string taskName, string cliExecutablePath, string scanPath, string exportPath, TimeOnly time)
    {
        var taskCommand = $"\\\"{cliExecutablePath}\\\" scan \\\"{scanPath}\\\" --html \\\"{exportPath}\\\"";
        var arguments = $"/Create /TN \"{taskName}\" /TR \"{taskCommand}\" /SC DAILY /ST {time:HH:mm} /F";
        RunSchTasks(arguments);
    }

    public static void DeleteTask(string taskName)
    {
        RunSchTasks($"/Delete /TN \"{taskName}\" /F");
    }

    private static void RunSchTasks(string arguments)
    {
        var startInfo = new ProcessStartInfo("schtasks.exe", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Impossible de démarrer schtasks.exe.");

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"schtasks.exe a échoué (code {process.ExitCode}) : {error}");
        }
    }
}
