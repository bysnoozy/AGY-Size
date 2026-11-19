using AgySize.Core.Models;

namespace AgySize.Core.Sharing;

/// <summary>Un partage et, s'il se trouve dans l'arborescence analysée, le dossier correspondant.</summary>
public sealed record ShareCoverage(NetworkShare Share, FileSystemNode? Node)
{
    public bool IsCovered => Node is not null;
}

public static class ShareMatcher
{
    /// <summary>
    /// Associe chaque partage au dossier correspondant de l'analyse (comparaison de chemins insensible
    /// à la casse, comme sous Windows). Si l'analyse a porté directement sur un chemin UNC
    /// (\\serveur\partage...), ce partage est déduit du chemin analysé, sans interroger la machine.
    /// </summary>
    public static IReadOnlyList<ShareCoverage> Match(ScanResult result, IEnumerable<NetworkShare> localShares)
    {
        var root = result.RootNode;
        var rootPath = Normalize(root.FullPath);

        if (rootPath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            var parts = rootPath.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var unc = $@"\\{parts[0]}\{parts[1]}";
                return new[] { new ShareCoverage(new NetworkShare(parts[1], unc, root.FullPath), root) };
            }
        }

        var matches = new List<ShareCoverage>();
        foreach (var share in localShares)
        {
            var sharePath = Normalize(share.LocalPath);
            FileSystemNode? node = null;

            if (string.Equals(sharePath, rootPath, StringComparison.OrdinalIgnoreCase))
            {
                node = root;
            }
            else if (sharePath.StartsWith(rootPath + @"\", StringComparison.OrdinalIgnoreCase))
            {
                node = FindIgnoreCase(root, sharePath[(rootPath.Length + 1)..].Split('\\'));
            }

            matches.Add(new ShareCoverage(share, node));
        }

        return matches;
    }

    /// <summary>
    /// Partages couverts qui ne sont pas inclus dans un autre partage couvert : base des totaux,
    /// pour ne pas compter deux fois un partage imbriqué dans un autre.
    /// </summary>
    public static IReadOnlyList<ShareCoverage> TopLevel(IEnumerable<ShareCoverage> covered)
    {
        var list = covered.Where(c => c.IsCovered).ToList();
        return list.Where(c => !list.Any(o => !ReferenceEquals(o, c) && o.Node != c.Node && IsAncestor(o.Node!, c.Node!)))
            .DistinctBy(c => c.Node)
            .ToList();
    }

    private static bool IsAncestor(FileSystemNode ancestor, FileSystemNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current == ancestor)
            {
                return true;
            }
        }

        return false;
    }

    private static FileSystemNode? FindIgnoreCase(FileSystemNode root, IEnumerable<string> segments)
    {
        var current = root;
        foreach (var segment in segments)
        {
            current = current.Children.Find(c =>
                c.Kind == FileSystemNodeKind.Folder && string.Equals(c.Name, segment, StringComparison.OrdinalIgnoreCase));
            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static string Normalize(string path) => path.Replace('/', '\\').TrimEnd('\\');
}
