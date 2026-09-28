using AgySize.Core.Models;

namespace AgySize.Core.Audit.Rules;

/// <summary>
/// Flags names that are structurally invalid for SharePoint: leading/trailing spaces, a trailing
/// period, consecutive periods in the middle of the name, names that are too long, and names that
/// collide with a reserved Windows or SharePoint name.
/// </summary>
public sealed class InvalidNameRule : IAuditRule
{
    public IEnumerable<AuditIssue> Evaluate(FileSystemNode entry, ScanOptions options)
    {
        var name = entry.Name;

        if (name.StartsWith(' ') || name.EndsWith(' '))
        {
            yield return Issue(entry, AuditIssueType.NameStartsOrEndsWithSpace,
                "Le nom commence ou se termine par un espace, ce qui n'est pas autorisé par SharePoint.");
        }

        if (name.EndsWith('.'))
        {
            yield return Issue(entry, AuditIssueType.NameEndsWithPeriod,
                "Le nom se termine par un point, ce qui n'est pas autorisé par SharePoint.");
        }

        if (name.Contains(".."))
        {
            yield return Issue(entry, AuditIssueType.ConsecutivePeriodsInName,
                "Le nom contient des points consécutifs (\"..\"), ce qui n'est pas autorisé par SharePoint.");
        }

        if (name.Length > options.MaxNameLength)
        {
            yield return Issue(entry, AuditIssueType.NameTooLong,
                $"Le nom comporte {name.Length} caractères, ce qui dépasse la limite de " +
                $"{options.MaxNameLength} caractères.");
        }

        // Noms de périphériques Windows (CON, LPT1...) : réservés même avec une extension ("CON.txt"),
        // Windows ne regarde que la partie avant le premier point.
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(name);
        var isReservedDeviceName = SharePointLimits.ReservedDeviceNames.Contains(name)
            || SharePointLimits.ReservedDeviceNames.Contains(nameWithoutExtension);

        // Noms réservés exacts (fichiers système SharePoint, verrous Office) : comparés au nom complet
        // uniquement — ne pas retirer l'extension, sous peine de faux positif (ex. "notes.lock" n'est
        // pas le fichier réservé ".lock").
        var isReservedExactName = SharePointLimits.ReservedExactNames.Contains(name);

        var isReservedPrefix = SharePointLimits.ReservedNamePrefixes.Any(
            prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        // SharePoint additionally refuses folder names starting with "~" outright, beyond the "~$"
        // Office lock-file prefix (which applies to any entry, folders included).
        var isReservedFolderPrefix = entry.Kind == FileSystemNodeKind.Folder && name.StartsWith('~');

        // Un dossier "Forms" à la racine d'une bibliothèque entre en conflit avec le dossier système
        // que SharePoint y crée lui-même ; ce n'est un problème qu'au premier niveau (Depth == 1).
        var isReservedRootFormsFolder = entry.Kind == FileSystemNodeKind.Folder
            && entry.Depth == 1
            && string.Equals(name, "Forms", StringComparison.OrdinalIgnoreCase);

        if (isReservedDeviceName || isReservedExactName || isReservedPrefix || isReservedFolderPrefix || isReservedRootFormsFolder)
        {
            yield return Issue(entry, AuditIssueType.ReservedName,
                "Le nom correspond à un nom réservé (nom de périphérique Windows, nom système SharePoint, " +
                "fichier de verrouillage Office, dossier commençant par « ~ », ou dossier « Forms » à la " +
                "racine) non autorisé par SharePoint.");
        }
    }

    private static AuditIssue Issue(FileSystemNode entry, AuditIssueType type, string description) => new()
    {
        Type = type,
        Severity = AuditSeverity.Blocking,
        RelativePath = entry.RelativePath,
        Description = description,
    };
}
