using AgySize.Core.Models;

namespace AgySize.Core.Scanning;

public static class FileSystemNodeExtensions
{
    public static IEnumerable<FileSystemNode> Flatten(this FileSystemNode root)
    {
        yield return root;
        foreach (var child in root.Children)
        {
            foreach (var descendant in child.Flatten())
            {
                yield return descendant;
            }
        }
    }

    public static IEnumerable<FileSystemNode> Files(this FileSystemNode root) =>
        root.Flatten().Where(n => n.Kind == FileSystemNodeKind.File);

    public static IEnumerable<FileSystemNode> Folders(this FileSystemNode root) =>
        root.Flatten().Where(n => n.Kind == FileSystemNodeKind.Folder);
}
