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

    // Seuils de l'audit de compatibilité SharePoint Online / OneDrive (voir AgySize.Core.Audit),
    // alignés sur la documentation Microsoft au moment de l'écriture — à ajuster ici si Microsoft
    // fait évoluer ces limites, ou pour un tenant configuré différemment.

    /// <summary>
    /// Longueur maximale, en caractères, d'une URL SharePoint complète (site + bibliothèque + chemin
    /// + nom de fichier). Limite documentée par Microsoft : 400 caractères, chemin décodé.
    /// </summary>
    public int MaxFullUrlLength { get; init; } = 400;

    /// <summary>
    /// Nombre de caractères réservés pour l'URL du site de destination, le nom de la bibliothèque et
    /// un éventuel préfixe, avant le chemin de dossier scanné. Soustrait de
    /// <see cref="MaxFullUrlLength"/> pour calculer le budget disponible pour le chemin relatif.
    /// </summary>
    public int ReservedUrlPrefixLength { get; init; } = 100;

    /// <summary>
    /// Longueur maximale, en caractères, d'un seul nom de fichier ou de dossier (limite Microsoft :
    /// 255 caractères, distincte de la limite de 400 caractères sur le chemin complet).
    /// </summary>
    public int MaxNameLength { get; init; } = 255;

    /// <summary>
    /// Taille de fichier maximale, en octets. Par défaut 250 Go (limite de téléversement SharePoint
    /// Online / OneDrive au moment de l'écriture).
    /// </summary>
    public long MaxFileSizeInBytes { get; init; } = 250L * 1024 * 1024 * 1024;

    /// <summary>
    /// Profondeur de dossier au-delà de laquelle un avertissement est levé, car une imbrication
    /// profonde est une cause fréquente de dépassement de longueur de chemin. Heuristique
    /// configurable, pas une limite SharePoint documentée.
    /// </summary>
    public int MaxFolderDepth { get; init; } = 50;

    public bool IncludeBlockedFileTypeCheck { get; init; } = true;

    /// <summary>
    /// Extensions de fichier supplémentaires (avec le point, ex. ".xyz") à traiter comme bloquées, en
    /// plus de la liste intégrée <see cref="Audit.Rules.SharePointLimits.BlockedFileExtensions"/>.
    /// </summary>
    public IReadOnlySet<string>? ExtraBlockedExtensions { get; init; }

    public int EffectiveMaxRelativePathLength => Math.Max(0, MaxFullUrlLength - ReservedUrlPrefixLength);
}
