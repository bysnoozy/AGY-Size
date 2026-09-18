# Binaires prêts à l'emploi

Ce dossier contient les derniers binaires Windows construits par la CI, committés directement dans
le dépôt pour être installés sans passer par l'onglet Actions :

- **`AGY-Size-Setup.msi`** — installeur (raccourci menu Démarrer + démarrage automatique avec
  Windows configurés à l'installation). C'est le mode d'installation recommandé.
- **`AGY-Size-portable-win-x64.zip`** — exécutable autonome (`AgySize.App.exe` + `AgySize.Cli.exe`),
  à dézipper et lancer directement, sans installation.

## Mettre à jour ces fichiers

Ils ne sont **pas** régénérés automatiquement à chaque commit (pour ne pas alourdir le dépôt de
~100 Mo à chaque push pendant le développement). Pour publier une nouvelle version :

1. Dans l'onglet **Actions** du dépôt GitHub, ouvrir le workflow **"Publish release binaries to
   repo"**.
2. Cliquer sur **"Run workflow"** (branche `main`).
3. Une fois le run terminé, les fichiers de ce dossier sont mis à jour par un commit automatique.
