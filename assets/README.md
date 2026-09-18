# Assets AGY-Size

- `agytek-logo.png` : logo AGYTEK original (dégradé bleu → gris), fourni par l'utilisateur.
- `logo.png` : même logo, recadré au plus près (fond transparent) — copié dans
  `src/AgySize.App/Assets/logo.png` pour le bandeau d'en-tête de l'application.
- `logo.ico` : icône carrée multi-résolutions (16/24/32/48/64/128/256 px) générée à partir du logo
  (wordmark centré sur fond bleu nuit `#1B3A63`, cohérent avec le bandeau d'en-tête) — copiée dans
  `src/AgySize.App/Assets/logo.ico` pour l'icône de l'exécutable et de la fenêtre.

Le logo est déjà branché dans l'application :
- `src/AgySize.App/AgySize.App.csproj` référence `Assets\logo.ico` comme `ApplicationIcon` et déclare
  les deux fichiers comme `AvaloniaResource`.
- `src/AgySize.App/MainWindow.axaml` utilise `avares://AgySize.App/Assets/logo.ico` comme icône de
  fenêtre et affiche `avares://AgySize.App/Assets/logo.png` dans le bandeau d'en-tête.

Pour remplacer le logo plus tard : déposer un nouveau fichier ici, régénérer `logo.ico`/`logo.png` si
besoin (un simple recadrage/export suffit), puis copier les deux fichiers dans
`src/AgySize.App/Assets/` en écrasant les existants.
