using Avalonia;

namespace AgySize.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogCrash(e.Exception);
            e.SetObserved();
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogCrash(ex);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();

    /// <summary>
    /// Dernier filet de sécurité : sans ceci, un plantage disparaît silencieusement (pas de console
    /// attachée en mode WinExe) et il devient impossible de savoir ce qui s'est passé.
    /// </summary>
    private static void LogCrash(Exception? ex)
    {
        if (ex is null)
        {
            return;
        }

        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AGY-Size", "logs");
            Directory.CreateDirectory(logDirectory);
            File.AppendAllText(
                Path.Combine(logDirectory, "crash.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Ne jamais planter dans le gestionnaire de plantage lui-même.
        }
    }
}
