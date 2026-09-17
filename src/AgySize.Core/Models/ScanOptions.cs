namespace AgySize.Core.Models;

public sealed class ScanOptions
{
    public required string RootPath { get; init; }

    /// <summary>
    /// Active l'analyse des droits NTFS (propriétaire, héritage, ACL larges) pendant le scan.
    /// Désactivée par défaut car nettement plus lente sur de grosses arborescences, et sans effet
    /// hors Windows.
    /// </summary>
    public bool AnalyzePermissions { get; init; }

    /// <summary>
    /// Ancienneté, en jours, au-delà de laquelle un fichier est listé dans la vue "Fichiers anciens".
    /// </summary>
    public int OldFileThresholdDays { get; init; } = 365;
}
