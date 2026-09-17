namespace AgySize.App.ViewModels;

// Lignes d'affichage pré-formatées pour les DataGrid des différents onglets. Des classes simples
// suffisent : les listes sont reconstruites entièrement à chaque scan / recherche, pas besoin de
// suivre des changements individuels.

public sealed class ExtensionRow
{
    public required string Extension { get; init; }
    public required string FileCount { get; init; }
    public required string Size { get; init; }
    public long SizeInBytes { get; init; }
}

public sealed class LargestItemRow
{
    public required string RelativePath { get; init; }
    public required string Kind { get; init; }
    public required string Size { get; init; }
    public required FileSystemNodeViewModel Node { get; init; }
}

public sealed class OldFileRow
{
    public required string RelativePath { get; init; }
    public required string LastModified { get; init; }
    public required string Size { get; init; }
    public required FileSystemNodeViewModel Node { get; init; }
}

public sealed class EmptyFolderRow
{
    public required string RelativePath { get; init; }
    public required FileSystemNodeViewModel Node { get; init; }
}

public sealed class DuplicateRow
{
    public required string Groupe { get; init; }
    public required string RelativePath { get; init; }
    public required string Size { get; init; }
}

public sealed class LogRow
{
    public required string Time { get; init; }
    public required string Level { get; init; }
    public required string Source { get; init; }
    public required string Message { get; init; }
}
