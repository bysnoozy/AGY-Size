using AgySize.Core.Audit.Rules;
using AgySize.Core.Models;

namespace AgySize.Core.Audit.Remediation;

/// <summary>
/// Calcule un nom de remplacement conforme pour les catégories d'anomalies purement mécaniques
/// (caractères interdits, espace/point en trop, nom trop long) : celles où retirer/remplacer quelques
/// caractères résout le problème sans perte d'information ni ambiguïté. Les autres catégories
/// (<see cref="AuditIssueType.ReservedName"/>, <see cref="AuditIssueType.PathTooLong"/>,
/// <see cref="AuditIssueType.FolderTooDeep"/>, <see cref="AuditIssueType.FileTooLarge"/>,
/// <see cref="AuditIssueType.BlockedFileType"/>, <see cref="AuditIssueType.DuplicateNameDifferingByCase"/>)
/// demandent une décision humaine (déplacer, renommer différemment, changer de type de fichier...) et
/// ne sont donc jamais "auto-fixables" ici.
/// </summary>
public static class NameSanitizer
{
    private static readonly HashSet<AuditIssueType> FixableTypes = new()
    {
        AuditIssueType.InvalidCharacterInName,
        AuditIssueType.NameStartsOrEndsWithSpace,
        AuditIssueType.NameEndsWithPeriod,
        AuditIssueType.ConsecutivePeriodsInName,
        AuditIssueType.NameTooLong,
    };

    public static bool IsAutoFixable(AuditIssueType type) => FixableTypes.Contains(type);

    /// <summary>
    /// Propose un nom corrigé prenant en compte toutes les anomalies mécaniques détectées sur ce
    /// nom (un même fichier peut cumuler plusieurs catégories, ex. espace final + caractère interdit) :
    /// un seul renommage suffit alors à toutes les résoudre. Renvoie <c>null</c> si aucune des
    /// anomalies fournies n'est auto-fixable, ou si le nom obtenu serait vide.
    /// </summary>
    public static string? SuggestFixedName(string currentName, IEnumerable<AuditIssueType> issueTypes, int maxNameLength)
    {
        var types = new HashSet<AuditIssueType>(issueTypes.Where(IsAutoFixable));
        if (types.Count == 0)
        {
            return null;
        }

        var name = currentName;

        if (types.Contains(AuditIssueType.InvalidCharacterInName))
        {
            var chars = name.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(SharePointLimits.InvalidNameCharacters, chars[i]) >= 0)
                {
                    chars[i] = '_';
                }
            }

            name = new string(chars);
        }

        if (types.Contains(AuditIssueType.ConsecutivePeriodsInName))
        {
            while (name.Contains(".."))
            {
                name = name.Replace("..", ".");
            }
        }

        if (types.Contains(AuditIssueType.NameStartsOrEndsWithSpace))
        {
            name = name.Trim(' ');
        }

        if (types.Contains(AuditIssueType.NameEndsWithPeriod))
        {
            name = name.TrimEnd('.');
        }

        if (types.Contains(AuditIssueType.NameTooLong) && name.Length > maxNameLength)
        {
            var extension = Path.GetExtension(name);
            var stem = Path.GetFileNameWithoutExtension(name);
            var keep = Math.Max(1, maxNameLength - extension.Length);
            name = stem.Length > keep ? stem[..keep] + extension : name;
        }

        // Une troncature ou un remplacement de caractère peut réintroduire un espace/point final ;
        // un dernier nettoyage garantit un résultat propre quel que soit l'ordre des transformations.
        name = name.Trim(' ').TrimEnd('.');

        return name.Length > 0 && name != currentName ? name : null;
    }
}
