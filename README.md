# AGY-Size

Application de bureau (.NET 8 / Avalonia UI) d'analyse d'espace disque façon **TreeSize**, avec en
plus un **audit de compatibilité pour une migration SharePoint Online / OneDrive**, une **revue des
droits d'accès NTFS** et une **partie journal (logs)** complète — thème gris et bleu, aux couleurs
d'AGYTEK.

AGY-Size regroupe en une seule application ce qui était auparavant réparti entre deux outils : le
scanner de tailles/droits/logs d'origine, et l'audit de migration SharePoint du projet AuditFiles
(désormais fusionné ici — voir « Audit SharePoint » ci-dessous).

## Fonctionnalités

- **Arborescence des tailles** : scan récursif d'un dossier avec tailles agrégées par dossier
  (fichiers/sous-dossiers), triées par taille décroissante, barre de progression par élément.
- **Mise en conformité SharePoint Online / OneDrive**, dans une section dédiée et séparée de
  l'analyse d'espace disque (bascule en haut des onglets) : audit évalué automatiquement à chaque
  scan (sans case à cocher séparée) — chemins trop longs, caractères interdits, noms
  structurellement invalides (espace en début/fin, point final, points consécutifs, nom trop long),
  noms réservés (périphériques Windows, fichiers système SharePoint, dossier "Forms" à la racine),
  types de fichiers bloqués à l'upload, fichiers dépassant la limite de taille, dossiers trop
  imbriqués, doublons de noms ne différant que par la casse. Seuils alignés sur la documentation
  Microsoft, ajustables dans le code (`AgySize.Core.Audit.Rules.SharePointLimits` / `ScanOptions`).
  Pour faciliter la mise en conformité avant une migration réelle :
  - **récapitulatif par catégorie** (nombre d'occurrences, dont combien sont corrigibles
    automatiquement) ;
  - **filtre texte** (chemin, description, type) et case "corrigibles uniquement" pour se concentrer
    sur un sous-ensemble ;
  - **correction automatique en un clic** (renommage) pour les anomalies purement mécaniques —
    espace/point en trop, points consécutifs, caractère interdit, nom trop long — combinant toutes
    les anomalies cumulées sur un même nom en un seul renommage, disponible par élément (clic droit
    → "Corriger automatiquement") ou en masse sur tout ce qui est actuellement affiché ; les
    catégories nécessitant une décision humaine (chemin trop long, type bloqué, nom réservé,
    doublon...) restent manuelles, avec un accès direct via "Ouvrir l'emplacement" ;
  - export dédié en CSV, section propre dans le rapport HTML, et **rapport PDF client** prêt à
    remettre (synthèse + action recommandée et liste complète par catégorie d'anomalie).
- **Treemap** : visualisation en rectangles proportionnels à la taille, avec navigation par clic
  pour descendre dans un sous-dossier (algorithme "squarified treemap").
- **Répartition par type de fichier** (extension, nombre de fichiers, taille).
- **Plus gros dossiers / fichiers** de l'arborescence analysée.
- **Fichiers anciens** : fichiers non modifiés depuis plus d'un an (seuil configurable).
- **Dossiers vides**.
- **Recherche de doublons** (à la demande) : regroupement par taille, puis hachage partiel (64
  premiers Ko, pour écarter à moindre coût les fichiers qui partagent une taille par coïncidence)
  et enfin hachage SHA-256 complet, ces deux étapes de hachage étant menées en parallèle sur
  plusieurs fichiers à la fois ; progression affichée en direct pendant la recherche.
- **Suppression / déplacement** de fichiers ou dossiers directement depuis l'arborescence (corbeille
  Windows quand c'est possible), avec mise à jour immédiate des tailles affichées.
- **Revue des droits d'accès NTFS** (Windows, activable via une case à cocher — plus lent) :
  propriétaire de chaque dossier, héritage des autorisations cassé, identités génériques ("Tout le
  monde", "Utilisateurs authentifiés"...) disposant de droits étendus, refus explicites.
- **Journal (logs)** :
  - panneau "Journal" dans l'application, mis à jour en direct pendant le scan (progression, erreurs
    de lecture, suppressions/déplacements, résultats de recherche de doublons...) ;
  - fichier de log persistant sur disque (`%LocalAppData%\AGY-Size\logs\agysize.log`), qui accumule
    l'historique entre les lancements ;
  - export du journal affiché vers un fichier texte.
- **Export** CSV (arborescence complète, ou anomalies SharePoint seules) et HTML (rapport de
  synthèse : volumétrie, plus gros éléments, répartition par type, anomalies SharePoint, constats sur
  les droits), en plus du rapport PDF client dédié à l'audit SharePoint.
- **Démarrage automatique avec Windows**, activable/désactivable depuis l'application (clé de
  registre `Run` de l'utilisateur courant). L'application elle-même nécessite une élévation
  administrateur à chaque lancement (voir ci-dessous) : Windows affichera une invite UAC au
  démarrage automatique comme à un lancement manuel.
- **Scans planifiés** : bouton "Planifier" qui enregistre une tâche quotidienne dans le
  Planificateur de tâches Windows, exécutant `AgySize.Cli` et exportant un rapport HTML.
- **Ligne de commande** (`AgySize.Cli`) pour scripter un scan : `agysize scan <dossier> [--csv f]
  [--html f] [--audit-csv f] [--audit-pdf f] [--permissions]`.

L'application cible avant tout **Windows** (ACL NTFS, registre, corbeille, planificateur de tâches),
mais s'appuie sur Avalonia UI plutôt que WPF afin de pouvoir être compilée et testée en mode
headless sur n'importe quelle plateforme (Linux/macOS inclus) ; les fonctionnalités spécifiquement
Windows sont protégées par des vérifications `OperatingSystem.IsWindows()` et se désactivent
proprement ailleurs.

## Structure du projet

```
AgySize.sln
src/
  AgySize.Core/    Bibliothèque .NET 8 (multiplateforme) : scanner, arbre de tailles, doublons,
                    opérations fichiers, droits NTFS, logs, audit SharePoint (Audit/Rules), exports
                    CSV/HTML/PDF, démarrage auto, planification.
  AgySize.App/      Application de bureau Avalonia UI (net8.0) qui s'appuie sur AgySize.Core.
  AgySize.Cli/      Utilitaire en ligne de commande (scans scriptés/planifiés).
tests/
  AgySize.Core.Tests/  Tests unitaires (xUnit) du scanner, des règles d'audit SharePoint, des
                        doublons, des opérations fichiers et des exports (dont le PDF client).
installer/          Projet WiX v4 pour un installeur .msi (voir installer/README.md — expérimental).
assets/             Logo AGYTEK (voir assets/README.md pour l'intégrer).
```

## Compiler et exécuter

Nécessite le [SDK .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
# Compiler toute la solution
dotnet build AgySize.sln

# Lancer les tests de la bibliothèque Core
dotnet test tests/AgySize.Core.Tests/AgySize.Core.Tests.csproj

# Lancer l'application graphique sur la plateforme courante
dotnet run --project src/AgySize.App/AgySize.App.csproj

# Scanner en ligne de commande
dotnet run --project src/AgySize.Cli/AgySize.Cli.csproj -- scan C:\Partage --html rapport.html

# Produire un exécutable Windows autonome (fonctionne même depuis Linux/macOS)
dotnet publish src/AgySize.App/AgySize.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Mode installation

Binaires Windows prêts à l'emploi, directement dans le dépôt : voir `releases/` (`AGY-Size-Setup.msi`
pour une installation classique, ou `AGY-Size-portable-win-x64.zip` pour l'exécutable autonome sans
installation). Voir `releases/README.md` pour savoir comment les régénérer.

Ces mêmes binaires sont aussi (re)construits à chaque push sur `main` par la CI, sans être committés :
- **Mode `.exe`** : artefact `AGY-Size-win-x64` du job `publish-windows`.
- **Mode `.msi`** : artefact `AGY-Size-installer` du job `build-installer`.

Dans les deux cas, le démarrage automatique avec Windows est activable depuis l'application (case à
cocher "Démarrer avec Windows"), en plus de celui déjà configuré par l'installeur `.msi`.

### Signature des binaires

Les exécutables et l'installeur sont signés (Authenticode) avec un certificat **auto-signé AGYTEK**,
pour un usage interne. Voir [`docs/CODE_SIGNING.md`](docs/CODE_SIGNING.md) pour la mise en place des
secrets CI et l'installation du certificat de confiance sur les postes AGYTEK.

## Intégration continue

`.github/workflows/build.yml` :
- compile et teste la bibliothèque Core et l'application Avalonia sous Linux ;
- publie les exécutables Windows autonomes (`AgySize.App.exe`, `AgySize.Cli.exe`) sous Windows ;
- construit (best-effort) l'installeur `.msi` sous Windows.

## Limites connues et pistes futures

- Le scan lit les métadonnées du système de fichiers local (ou d'un partage réseau monté).
- Microsoft fait évoluer les règles de compatibilité SharePoint/OneDrive (ex. `#`/`%` étaient bloqués
  par défaut et sont désormais autorisés) : vérifiez les seuils dans
  `AgySize.Core.Audit.Rules.SharePointLimits` / `ScanOptions` sur la documentation Microsoft 365 à
  jour avant de vous fier au rapport pour une décision de migration réelle.
- La revue des droits NTFS ne fonctionne que sous Windows ; elle est désactivée par défaut (case à
  cocher) car elle ralentit sensiblement le scan sur de grosses arborescences.
- Après une suppression/déplacement effectué depuis un onglet autre que "Arborescence" (ex. "Plus
  gros éléments"), les totaux globaux se mettent à jour immédiatement, mais les lignes déjà
  affichées dans l'arborescence ne se rafraîchissent qu'au prochain scan. Même principe pour la
  correction automatique des anomalies SharePoint (renommage) : l'élément corrigé disparaît de la
  liste des anomalies, mais un nouveau scan est nécessaire pour un audit complètement à jour (par
  exemple si le renommage résout aussi un "chemin trop long" plus haut dans l'arborescence).
- La planification de scans nécessite que `AgySize.Cli.exe` soit publié à côté de l'application.
- Pas d'agent/service en tâche de fond pour l'instant : AGY-Size est une application de bureau
  classique (avec démarrage automatique optionnel). Un agent est envisagé pour une itération future.
- Si l'application se ferme de façon inattendue (plantage), un fichier
  `%LocalAppData%\AGY-Size\logs\crash.log` est créé avec le détail de l'exception : à joindre en cas
  de rapport de bug, en plus du journal normal (`agysize.log` dans le même dossier).
- **AgySize.App nécessite une élévation administrateur à chaque lancement** (manifeste
  `requireAdministrator`) : la suppression/le déplacement de fichiers et la lecture des ACL NTFS sur
  des dossiers partagés/protégés en dépendent souvent. Conséquence : Windows affiche une invite UAC à
  chaque démarrage, y compris via le démarrage automatique ou un raccourci ; sur un poste où
  l'utilisateur n'est pas administrateur local, l'application ne pourra pas du tout se lancer sans
  qu'un administrateur saisisse ses identifiants. `AgySize.Cli.exe` (scans planifiés) n'est pas
  concerné : il continue de s'exécuter sans élévation.
