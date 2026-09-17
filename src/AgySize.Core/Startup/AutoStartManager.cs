using System.Runtime.Versioning;
using Microsoft.Win32;

namespace AgySize.Core.Startup;

/// <summary>
/// Gère le démarrage automatique d'AGY-Size avec Windows via la clé Run de l'utilisateur courant
/// (pas de droits administrateur requis, effectif dès la prochaine ouverture de session).
/// </summary>
[SupportedOSPlatform("windows")]
public static class AutoStartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AGY-Size";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is not null;
    }

    public static void SetEnabled(bool enabled, string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (enabled)
        {
            key.SetValue(ValueName, $"\"{executablePath}\" --minimized", RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
