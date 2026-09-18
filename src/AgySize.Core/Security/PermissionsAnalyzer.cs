using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using AgySize.Core.Models;

namespace AgySize.Core.Security;

/// <summary>
/// Analyse les autorisations NTFS d'un dossier : propriétaire, héritage cassé, identités génériques
/// disposant de droits étendus, refus explicites. Windows uniquement (les ACL NTFS n'ont pas
/// d'équivalent direct sur les autres systèmes) : l'appelant doit vérifier
/// <see cref="OperatingSystem.IsWindows"/> avant d'invoquer cette classe.
/// </summary>
[SupportedOSPlatform("windows")]
public static class PermissionsAnalyzer
{
    /// <summary>
    /// Identités génériques dont l'octroi de droits d'écriture/modification mérite une vérification
    /// avant une migration ou un audit de sécurité (noms Windows en anglais et en français selon la
    /// langue du système).
    /// </summary>
    private static readonly string[] BroadIdentityNames =
    {
        "Everyone",
        "Tout le monde",
        "Authenticated Users",
        "Utilisateurs authentifiés",
        "BUILTIN\\Users",
        "Utilisateurs",
        "Domain Users",
        "Utilisateurs du domaine",
    };

    private const FileSystemRights RiskyRights =
        FileSystemRights.Modify | FileSystemRights.FullControl | FileSystemRights.Write | FileSystemRights.WriteData;

    /// <summary>
    /// Analyse un dossier et renseigne au passage <see cref="FileSystemNode.OwnerName"/>.
    /// </summary>
    public static IReadOnlyList<PermissionFinding> AnalyzeFolder(FileSystemNode node)
    {
        var findings = new List<PermissionFinding>();
        var displayPath = node.RelativePath.Length == 0 ? "(racine)" : node.RelativePath;

        try
        {
            var security = new DirectoryInfo(node.FullPath).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);

            var owner = security.GetOwner(typeof(NTAccount));
            node.OwnerName = owner?.Value;
            var inheritanceBroken = security.AreAccessRulesProtected;

            // Toujours un constat de base, même sans anomalie : sans cette ligne, un dossier aux
            // permissions saines ne produit rien et l'onglet "Droits d'accès" paraît vide/en panne
            // alors que l'analyse a bien tourné.
            findings.Add(new PermissionFinding
            {
                RelativePath = displayPath,
                Kind = FileSystemNodeKind.Folder,
                Severity = PermissionSeverity.Info,
                Category = PermissionFindingCategory.OwnerInfo,
                IdentityName = owner?.Value,
                Description = inheritanceBroken
                    ? "Héritage désactivé sur ce dossier (voir le constat détaillé ci-dessous)."
                    : "Aucune anomalie détectée (héritage actif, pas d'ACL étendue ni de refus explicite).",
            });

            if (inheritanceBroken)
            {
                findings.Add(new PermissionFinding
                {
                    RelativePath = displayPath,
                    Kind = FileSystemNodeKind.Folder,
                    Severity = PermissionSeverity.Warning,
                    Category = PermissionFindingCategory.BrokenInheritance,
                    Description = "L'héritage des autorisations est désactivé sur ce dossier : ses droits " +
                        "ne dépendent plus du dossier parent et doivent être vérifiés indépendamment.",
                });
            }

            var rules = security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(NTAccount));
            foreach (FileSystemAccessRule rule in rules)
            {
                var identityName = rule.IdentityReference.Value;

                if (rule.AccessControlType == AccessControlType.Deny)
                {
                    findings.Add(new PermissionFinding
                    {
                        RelativePath = displayPath,
                        Kind = FileSystemNodeKind.Folder,
                        Severity = PermissionSeverity.Info,
                        Category = PermissionFindingCategory.ExplicitDeny,
                        IdentityName = identityName,
                        Rights = rule.FileSystemRights.ToString(),
                        Description = $"Refus explicite pour '{identityName}' ({rule.FileSystemRights}).",
                    });
                    continue;
                }

                var isBroadIdentity = BroadIdentityNames.Any(
                    name => identityName.Contains(name, StringComparison.OrdinalIgnoreCase));
                var hasRiskyRights = (rule.FileSystemRights & RiskyRights) != 0;

                if (isBroadIdentity && hasRiskyRights)
                {
                    findings.Add(new PermissionFinding
                    {
                        RelativePath = displayPath,
                        Kind = FileSystemNodeKind.Folder,
                        Severity = PermissionSeverity.Critical,
                        Category = PermissionFindingCategory.BroadGrant,
                        IdentityName = identityName,
                        Rights = rule.FileSystemRights.ToString(),
                        Description = $"'{identityName}' dispose de droits étendus ({rule.FileSystemRights}) " +
                            "sur ce dossier.",
                    });
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or InvalidOperationException)
        {
            findings.Add(new PermissionFinding
            {
                RelativePath = displayPath,
                Kind = FileSystemNodeKind.Folder,
                Severity = PermissionSeverity.Warning,
                Category = PermissionFindingCategory.AnalysisError,
                Description = $"Impossible de lire les autorisations : {ex.Message}",
            });
        }

        return findings;
    }
}
