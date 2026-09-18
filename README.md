# AGY-Size

Application de bureau (.NET 8 / Avalonia UI) d'analyse d'espace disque façon **TreeSize**, avec en
plus une **revue des droits d'accès NTFS** et une **partie journal (logs)** complète — thème gris et
bleu, aux couleurs d'AGYTEK.

## Fonctionnalités

- **Arborescence des tailles** : scan récursif d'un dossier avec tailles agrégées par dossier
  (fichiers/sous-dossiers), triées par taille décroissante, barre de progression par élément.
- **Treemap** : visualisation en rectangles proportionnels à la taille, avec navigation par clic
  pour descendre dans un sous-dossier (algorithme "squarified treemap").
- **Répartition par type de fichier** (extension, nombre de fichiers, taille).
- **Plus gros dossiers / fichiers** de l'arborescence analysée.
- **Fichiers anciens** : fichiers non modifiés depuis plus d'un an (seuil configurable).
- **Dossiers vides**.
- **Recherche de doublons** (à la demande) : regroupement par taille puis hachage SHA-256.
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
- **Export** CSV (arborescence complète) et HTML (rapport de synthèse : volumétrie, plus gros
  éléments, répartition par type, constats sur les droits).
- **Démarrage automatique avec Windows**, activable/désactivable depuis l'application (clé de
  registre `Run` de l'utilisateur courant, sans droits administrateur).
- **Scans planifiés** : bouton "Planifier" qui enregistre une tâche quotidienne dans le
  Planificateur de tâches Windows, exécutant `AgySize.Cli` et exportant un rapport HTML.
- **Ligne de commande** (`AgySize.Cli`) pour scripter un scan : `agysize scan <dossier> [--csv f]
  [--html f] [--permissions]`.

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
                    opérations fichiers, droits NTFS, logs, exports CSV/HTML, démarrage auto,
                    planification.
  AgySize.App/      Application de bureau Avalonia UI (net8.0) qui s'appuie sur AgySize.Core.
  AgySize.Cli/      Utilitaire en ligne de commande (scans scriptés/planifiés).
tests/
  AgySize.Core.Tests/  Tests unitaires (xUnit) du scanner, des doublons, des opérations fichiers et
                        des exports.
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

## Intégration continue

`.github/workflows/build.yml` :
- compile et teste la bibliothèque Core et l'application Avalonia sous Linux ;
- publie les exécutables Windows autonomes (`AgySize.App.exe`, `AgySize.Cli.exe`) sous Windows ;
- construit (best-effort) l'installeur `.msi` sous Windows.

## Limites connues et pistes futures

- Le scan lit les métadonnées du système de fichiers local (ou d'un partage réseau monté).
- La revue des droits NTFS ne fonctionne que sous Windows ; elle est désactivée par défaut (case à
  cocher) car elle ralentit sensiblement le scan sur de grosses arborescences.
- Après une suppression/déplacement effectué depuis un onglet autre que "Arborescence" (ex. "Plus
  gros éléments"), les totaux globaux se mettent à jour immédiatement, mais les lignes déjà
  affichées dans l'arborescence ne se rafraîchissent qu'au prochain scan.
- La planification de scans nécessite que `AgySize.Cli.exe` soit publié à côté de l'application.
- Pas d'agent/service en tâche de fond pour l'instant : AGY-Size est une application de bureau
  classique (avec démarrage automatique optionnel). Un agent est envisagé pour une itération future.
- Si l'application se ferme de façon inattendue (plantage), un fichier
  `%LocalAppData%\AGY-Size\logs\crash.log` est créé avec le détail de l'exception : à joindre en cas
  de rapport de bug, en plus du journal normal (`agysize.log` dans le même dossier).
