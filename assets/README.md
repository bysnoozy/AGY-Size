# Assets AGY-Size

Déposez ici le logo AGYTEK :

- `logo.png` — utilisé dans l'en-tête de la fenêtre principale (bandeau gris/bleu).
- `logo.ico` — icône de l'exécutable Windows et de la fenêtre (multi-résolutions : 16/32/48/256 px).

Une fois les fichiers ajoutés :

1. Dans `src/AgySize.App/AgySize.App.csproj`, décommentez :
   ```xml
   <ApplicationIcon>Assets\logo.ico</ApplicationIcon>
   ```
2. Dans `src/AgySize.App/Views/MainWindow.axaml`, décommentez la balise `Icon="avares://AgySize.App/Assets/logo.ico"`
   sur l'élément `<Window>`, et l'`<Image Source="avares://AgySize.App/Assets/logo.png">` du bandeau d'en-tête
   (repérable par le commentaire `<!-- Logo AGYTEK -->`).
3. Ajoutez les fichiers en tant que ressource Avalonia dans `AgySize.App.csproj` :
   ```xml
   <ItemGroup>
     <AvaloniaResource Include="Assets\logo.png" />
     <AvaloniaResource Include="Assets\logo.ico" />
   </ItemGroup>
   ```

Ces étapes sont volontairement laissées en commentaire tant que les fichiers n'existent pas, pour ne
pas casser la compilation.
