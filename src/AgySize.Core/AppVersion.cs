using System.Reflection;

namespace AgySize.Core;

/// <summary>Version des binaires, telle que définie par Directory.Build.props (ex. "1.1.12 (f8573a5)").</summary>
public static class AppVersion
{
    public static string Display { get; } = Compute();

    private static string Compute()
    {
        var informational = typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrEmpty(informational))
        {
            return typeof(AppVersion).Assembly.GetName().Version?.ToString(3) ?? "?";
        }

        var plus = informational.IndexOf('+');
        if (plus < 0)
        {
            return informational;
        }

        var commit = informational[(plus + 1)..];
        return $"{informational[..plus]} ({commit[..Math.Min(7, commit.Length)]})";
    }
}
