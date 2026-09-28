using AgySize.Core.Models;

namespace AgySize.Core.Reporting;

/// <summary>
/// Client-facing label and recommended remediation action for each <see cref="AuditIssueType"/>,
/// used by the PDF client report. Written in plain language for a non-technical reader, unlike the
/// technical <see cref="AuditIssue.Description"/> strings used in the CSV/HTML exports.
/// </summary>
public static class RemediationAdvice
{
    public static (string Label, string Action) Get(AuditIssueType type) => type switch
    {
        AuditIssueType.PathTooLong => (
            "Chemins trop longs",
            "Raccourcir les chemins concernés : renommer les dossiers ou fichiers les plus longs, ou " +
            "simplifier l'arborescence, afin de respecter la limite de longueur d'URL imposée par SharePoint."),

        AuditIssueType.InvalidCharacterInName => (
            "Caractères interdits dans les noms",
            "Renommer les fichiers et dossiers concernés en supprimant les caractères non autorisés " +
            "(\" * : < > ? / \\ | { })."),

        AuditIssueType.NameStartsOrEndsWithSpace => (
            "Espaces en début ou fin de nom",
            "Renommer les éléments concernés en supprimant les espaces en début ou en fin de nom."),

        AuditIssueType.NameEndsWithPeriod => (
            "Nom se terminant par un point",
            "Renommer les éléments concernés en supprimant le point final."),

        AuditIssueType.ConsecutivePeriodsInName => (
            "Points consécutifs dans le nom",
            "Renommer les éléments concernés en remplaçant les points consécutifs (\"..\") par un point " +
            "unique ou un autre séparateur."),

        AuditIssueType.NameTooLong => (
            "Noms trop longs",
            "Raccourcir le nom des fichiers ou dossiers concernés (limite : 255 caractères par élément)."),

        AuditIssueType.ReservedName => (
            "Noms réservés",
            "Renommer les éléments concernés : leur nom est réservé par Windows ou par SharePoint " +
            "(ex. CON, PRN, fichiers temporaires ~$..., dossiers commençant par ~)."),

        AuditIssueType.BlockedFileType => (
            "Types de fichiers bloqués",
            "Convertir ces fichiers dans un format autorisé, les renommer, ou les exclure de la migration."),

        AuditIssueType.FileTooLarge => (
            "Fichiers trop volumineux",
            "Compresser, archiver ou exclure ces fichiers de la migration ; envisager une solution de " +
            "stockage complémentaire si nécessaire."),

        AuditIssueType.FolderTooDeep => (
            "Arborescence trop profonde",
            "Simplifier la structure des dossiers concernés pour réduire le niveau d'imbrication."),

        AuditIssueType.DuplicateNameDifferingByCase => (
            "Doublons ne différant que par la casse",
            "Renommer l'un des deux éléments en conflit : SharePoint ne distingue pas les majuscules " +
            "des minuscules."),

        _ => ("Anomalie", "Examiner et corriger les éléments concernés avant la migration."),
    };
}
