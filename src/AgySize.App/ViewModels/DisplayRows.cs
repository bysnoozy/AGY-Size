using AgySize.Core.Models;

namespace AgySize.App.ViewModels;

// Lignes d'affichage pré-formatées pour les DataGrid des différents onglets. Des classes simples
// suffisent : les listes sont reconstruites entièrement à chaque scan / recherche, pas besoin de
// suivre des changements individuels.

public sealed class ExtensionRow
{
    public required string Extension { get; init; }
    public required string FileCount { get; init; }
    public required string Size { get; init; }
    public long SizeInBytes { get; init; }
}

public sealed class LargestItemRow
{
    public required string RelativePath { get; init; }
    public required string Kind { get; init; }
    public required string Size { get; init; }
    public required FileSystemNodeViewModel Node { get; init; }
}

public sealed class OldFileRow
{
    public required string RelativePath { get; init; }
    public required string LastModified { get; init; }
    public required string Size { get; init; }
    public required FileSystemNodeViewModel Node { get; init; }
}

public sealed class EmptyFolderRow
{
    public required string RelativePath { get; init; }
    public required FileSystemNodeViewModel Node { get; init; }
}

public sealed class DuplicateRow
{
    public required string Groupe { get; init; }
    public required string RelativePath { get; init; }
    public required string Size { get; init; }
    public required FileSystemNodeViewModel Node { get; init; }
}

public sealed class LogRow
{
    public required string Time { get; init; }
    public required string Level { get; init; }
    public required string Source { get; init; }
    public required string Message { get; init; }
}

/// <summary>
/// Ligne de l'onglet "Mise en conformité SharePoint" : une anomalie détectée, enrichie d'un lien vers
/// le nœud réel de l'arborescence (retrouvé par chemin, voir <see cref="FileSystemNode.FindByRelativePath"/>)
/// et, quand la catégorie s'y prête, d'un nom de remplacement suggéré prêt à appliquer en un clic.
/// </summary>
public sealed class AuditIssueRow
{
    public required AuditIssueType Type { get; init; }
    public required AuditSeverity Severity { get; init; }
    public required string RelativePath { get; init; }
    public required string Description { get; init; }
    public required string TypeDisplay { get; init; }
    public FileSystemNode? Node { get; init; }
    public string? SuggestedName { get; init; }
    public bool AutoFixable => SuggestedName is not null;
    public string AutoFixableDisplay => AutoFixable ? "✓" : "";
    public string SuggestionDisplay => SuggestedName ?? "—";
}

/// <summary>Récapitulatif par catégorie affiché en tête de l'onglet "Mise en conformité SharePoint".</summary>
public sealed class AuditCategorySummaryRow
{
    public required string TypeDisplay { get; init; }
    public required int Count { get; init; }
    public required int AutoFixableCount { get; init; }
}

public static class AuditIssueTypeDisplay
{
    public static string For(AuditIssueType type) => type switch
    {
        AuditIssueType.PathTooLong => "Chemin trop long",
        AuditIssueType.InvalidCharacterInName => "Caractère interdit",
        AuditIssueType.NameStartsOrEndsWithSpace => "Espace en début/fin de nom",
        AuditIssueType.NameEndsWithPeriod => "Point final",
        AuditIssueType.ConsecutivePeriodsInName => "Points consécutifs",
        AuditIssueType.NameTooLong => "Nom trop long",
        AuditIssueType.ReservedName => "Nom réservé",
        AuditIssueType.BlockedFileType => "Type de fichier bloqué",
        AuditIssueType.FileTooLarge => "Fichier trop volumineux",
        AuditIssueType.FolderTooDeep => "Dossier trop imbriqué",
        AuditIssueType.DuplicateNameDifferingByCase => "Doublon (casse différente)",
        _ => type.ToString(),
    };
}
