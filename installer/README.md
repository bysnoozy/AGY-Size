# Installeur MSI AGY-Size

Ce dossier contient un projet [WiX Toolset v4](https://wixtoolset.org/) qui construit un installeur
`.msi` pour AGY-Size : copie des fichiers publiés (récoltés via Heat/`HarvestDirectory`, cible x64),
raccourci menu Démarrer, et activation du démarrage automatique avec Windows (clé de registre `Run`
de l'utilisateur courant).

## Statut

Validé par la CI : le job `build-installer` (`.github/workflows/build.yml`) construit le `.msi` avec
succès sur `windows-latest` et le publie comme artefact (`AGY-Size-installer`). Il reste toutefois en
`continue-on-error: true` par prudence (aucune installation réelle n'a été testée sur un poste
Windows) — si une régression apparaît un jour, elle n'empêchera pas le reste de la CI de passer.

## Alternative disponible

Le job `publish-windows` produit aussi un exécutable Windows autonome (`AgySize.App.exe`,
single-file, self-contained) téléchargeable comme artefact de build : c'est le mode ".exe" (aucune
installation requise, on lance directement l'exécutable). Le démarrage automatique peut alors être
activé depuis l'application elle-même (case à cocher "Démarrer avec Windows" dans la fenêtre
principale).

## Compiler localement (sous Windows, avec le SDK .NET 8)

```powershell
dotnet publish ..\src\AgySize.App\AgySize.App.csproj -c Release -r win-x64 --self-contained true -o publish
dotnet tool install --global wix --version 4.0.5
wix extension add WixToolset.UI.wixext/4.0.5
dotnet build AgySize.Installer.wixproj -c Release -p:PublishDir=publish
```

Le `.msi` est produit dans `bin\x64\Release\`.
