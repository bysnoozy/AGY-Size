namespace AgySize.Core.Models;

public sealed class ScanProgress
{
    public required long FilesScanned { get; init; }

    public required long FoldersScanned { get; init; }

    public string? CurrentPath { get; init; }

    /// <summary>Étape préalable en cours (ex. lecture de la MFT), affichée à la place des compteurs si renseignée.</summary>
    public string? Phase { get; init; }
}
