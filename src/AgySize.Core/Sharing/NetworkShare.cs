namespace AgySize.Core.Sharing;

/// <summary>Partage réseau (SMB) d'un serveur : nom, chemin UNC et dossier local partagé.</summary>
public sealed record NetworkShare(string Name, string UncPath, string LocalPath, string? Remark = null);
