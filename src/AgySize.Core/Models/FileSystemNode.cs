namespace AgySize.Core.Models;

/// <summary>
/// Un nœud de l'arborescence scannée (dossier ou fichier). Pour un dossier, <see cref="SizeInBytes"/>,
/// <see cref="FileCount"/> et <see cref="FolderCount"/> sont agrégés récursivement sur l'ensemble des
/// descendants, à la manière de TreeSize.
/// </summary>
public sealed class FileSystemNode
{
    public required string Name { get; init; }

    public required string FullPath { get; init; }

    /// <summary>
    /// Chemin relatif à la racine analysée, avec des séparateurs '/'. Vide pour la racine elle-même.
    /// </summary>
    public required string RelativePath { get; init; }

    public required FileSystemNodeKind Kind { get; init; }

    public long SizeInBytes { get; set; }

    /// <summary>Nombre de fichiers descendants (0 pour un fichier lui-même n'ayant pas d'enfants).</summary>
    public int FileCount { get; set; }

    /// <summary>Nombre de sous-dossiers descendants (n'inclut pas le dossier lui-même).</summary>
    public int FolderCount { get; set; }

    public DateTime LastWriteUtc { get; init; }

    public int Depth { get; init; }

    public FileSystemNode? Parent { get; set; }

    public List<FileSystemNode> Children { get; } = new();

    /// <summary>
    /// Nom du propriétaire NTFS, renseigné uniquement lorsque l'analyse des droits est activée
    /// (Windows uniquement). Null sinon.
    /// </summary>
    public string? OwnerName { get; set; }

    public bool IsEmptyFolder => Kind == FileSystemNodeKind.Folder && FileCount == 0;
}
