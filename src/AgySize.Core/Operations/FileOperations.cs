using AgySize.Core.Models;

namespace AgySize.Core.Operations;

/// <summary>
/// Suppression et déplacement d'éléments scannés, avec répercussion immédiate sur les tailles
/// agrégées des dossiers ancêtres dans l'arbre en mémoire (pas besoin de relancer un scan complet
/// pour que les totaux affichés restent cohérents).
/// </summary>
public static class FileOperations
{
    /// <summary>
    /// Supprime un fichier ou un dossier. Sous Windows, <paramref name="useRecycleBin"/> envoie
    /// l'élément à la corbeille plutôt que de le supprimer définitivement.
    /// </summary>
    public static void Delete(FileSystemNode node, bool useRecycleBin)
    {
        if (node.Kind == FileSystemNodeKind.Folder)
        {
            if (useRecycleBin && OperatingSystem.IsWindows())
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                    node.FullPath,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
            else
            {
                Directory.Delete(node.FullPath, recursive: true);
            }
        }
        else
        {
            if (useRecycleBin && OperatingSystem.IsWindows())
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    node.FullPath,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
            else
            {
                File.Delete(node.FullPath);
            }
        }

        DetachFromTree(node);
    }

    public static void Move(FileSystemNode node, string destinationDirectory)
    {
        var destination = Path.Combine(destinationDirectory, node.Name);

        if (node.Kind == FileSystemNodeKind.Folder)
        {
            Directory.Move(node.FullPath, destination);
        }
        else
        {
            File.Move(node.FullPath, destination);
        }

        DetachFromTree(node);
    }

    /// <summary>
    /// Renomme un fichier ou un dossier dans son dossier parent (utilisé par la correction automatique
    /// des anomalies de conformité SharePoint). Si <paramref name="desiredName"/> existe déjà à cet
    /// emplacement, un suffixe numérique est ajouté avant l'extension jusqu'à trouver un nom libre.
    /// Renvoie le nom effectivement utilisé.
    /// </summary>
    /// <remarks>
    /// Contrairement à <see cref="Delete"/> et <see cref="Move"/>, l'élément reste physiquement dans le
    /// même dossier : les tailles/compteurs agrégés des ancêtres n'ont donc pas besoin d'être ajustés.
    /// <see cref="FileSystemNode.Name"/>/<see cref="FileSystemNode.FullPath"/>/
    /// <see cref="FileSystemNode.RelativePath"/> sont en lecture seule (<c>init</c>) et ne peuvent pas
    /// être mis à jour en place : ce nœud est simplement retiré de l'arbre en mémoire, un nouveau scan
    /// le fera réapparaître sous son nouveau nom.
    /// </remarks>
    public static string Rename(FileSystemNode node, string desiredName)
    {
        var parentDirectory = Path.GetDirectoryName(node.FullPath)
            ?? throw new InvalidOperationException("Impossible de déterminer le dossier parent.");

        var stem = Path.GetFileNameWithoutExtension(desiredName);
        var extension = Path.GetExtension(desiredName);
        var finalName = desiredName;
        var suffix = 1;
        while (File.Exists(Path.Combine(parentDirectory, finalName)) || Directory.Exists(Path.Combine(parentDirectory, finalName)))
        {
            finalName = $"{stem} ({suffix}){extension}";
            suffix++;
        }

        var destination = Path.Combine(parentDirectory, finalName);
        if (node.Kind == FileSystemNodeKind.Folder)
        {
            Directory.Move(node.FullPath, destination);
        }
        else
        {
            File.Move(node.FullPath, destination);
        }

        node.Parent?.Children.Remove(node);
        node.Parent = null;

        return finalName;
    }

    private static void DetachFromTree(FileSystemNode node)
    {
        var parent = node.Parent;
        if (parent is null)
        {
            return;
        }

        var size = node.SizeInBytes;
        var isFolder = node.Kind == FileSystemNodeKind.Folder;
        var folderCountDelta = isFolder ? node.FolderCount + 1 : 0;
        var fileCountDelta = isFolder ? node.FileCount : 1;

        var ancestor = parent;
        while (ancestor is not null)
        {
            ancestor.SizeInBytes -= size;
            ancestor.FolderCount -= folderCountDelta;
            ancestor.FileCount -= fileCountDelta;
            ancestor = ancestor.Parent;
        }

        parent.Children.Remove(node);
        node.Parent = null;
    }
}
