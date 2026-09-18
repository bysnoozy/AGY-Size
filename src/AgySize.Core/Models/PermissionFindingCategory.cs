namespace AgySize.Core.Models;

public enum PermissionFindingCategory
{
    /// <summary>
    /// Constat de base (propriétaire, état de l'héritage), ajouté pour chaque dossier analysé même
    /// en l'absence d'anomalie — sans cela, un dossier aux permissions saines ne produit aucune ligne
    /// et l'onglet "Droits d'accès" paraît vide/en panne alors que l'analyse a bien tourné.
    /// </summary>
    OwnerInfo,

    /// <summary>L'héritage des autorisations est désactivé sur ce dossier.</summary>
    BrokenInheritance,

    /// <summary>Une identité générique (Tout le monde, Utilisateurs authentifiés...) dispose de droits étendus.</summary>
    BroadGrant,

    /// <summary>Une règle de refus explicite est présente (cas rare, à vérifier).</summary>
    ExplicitDeny,

    /// <summary>Les autorisations n'ont pas pu être lues (accès refusé, erreur I/O...).</summary>
    AnalysisError,
}
