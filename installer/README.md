# Installeur MSI AGY-Size (expérimental)

Ce dossier contient un projet [WiX Toolset v4](https://wixtoolset.org/) qui construit un installeur
`.msi` pour AGY-Size : copie des fichiers publiés, raccourci menu Démarrer, et activation du
démarrage automatique avec Windows (clé de registre `Run` de l'utilisateur courant).

## Statut

Ce projet est **best-effort** : il a été écrit selon les conventions WiX v4 mais n'a pas pu être
compilé ni testé dans l'environnement de développement (pas de toolchain Windows/WiX disponible).
Le job `build-installer` du workflow CI (`.github/workflows/build.yml`) le construit sur
`windows-latest` avec `continue-on-error: true` : une éventuelle erreur n'empêche pas le reste de la
CI de passer, mais signifie que `Product.wxs` doit être corrigé avant de pouvoir distribuer un vrai
`.msi`.

## Alternative disponible dès maintenant

En attendant que l'installeur MSI soit validé, le job `publish-windows` produit un exécutable
Windows autonome (`AgySize.App.exe`, single-file, self-contained) téléchargeable comme artefact de
build : c'est le mode ".exe" (aucune installation requise, on lance directement l'exécutable). Le
démarrage automatique peut alors être activé depuis l'application elle-même (case à cocher
"Démarrer avec Windows" dans la fenêtre principale).

## Compiler localement (sous Windows, avec le SDK .NET 8)

```powershell
dotnet publish ..\src\AgySize.App\AgySize.App.csproj -c Release -r win-x64 --self-contained true -o publish
dotnet tool install --global wix --version 4.0.5
wix extension add WixToolset.UI.wixext/4.0.5
dotnet build AgySize.Installer.wixproj -c Release -p:PublishDir=publish
```

Le `.msi` est produit dans `bin\Release\`.
