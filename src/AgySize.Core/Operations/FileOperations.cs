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
