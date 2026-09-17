using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AgySize.Core.Models;

namespace AgySize.App.ViewModels;

/// <summary>
/// Habille un <see cref="FileSystemNode"/> pour l'affichage (arbre, treemap) : les enfants sont
/// matérialisés en <see cref="ObservableCollection{T}"/> à la demande seulement, pour ne pas
/// construire d'un coup des dizaines de milliers de ViewModels sur une grosse arborescence.
/// </summary>
public sealed class FileSystemNodeViewModel : INotifyPropertyChanged
{
    private readonly long _rootSizeInBytes;
    private ObservableCollection<FileSystemNodeViewModel>? _children;

    public FileSystemNodeViewModel(FileSystemNode model, long rootSizeInBytes, FileSystemNodeViewModel? parent = null)
    {
        Model = model;
        _rootSizeInBytes = rootSizeInBytes;
        Parent = parent;
    }

    public FileSystemNode Model { get; }

    public FileSystemNodeViewModel? Parent { get; }

    public string Name => Model.Name;

    public string RelativePath => Model.RelativePath;

    public FileSystemNodeKind Kind => Model.Kind;

    public long SizeInBytes => Model.SizeInBytes;

    public string SizeDisplay => FormatBytes(Model.SizeInBytes);

    public double PercentOfRoot => _rootSizeInBytes <= 0 ? 0 : Math.Min(100, (double)Model.SizeInBytes / _rootSizeInBytes * 100);

    public string PercentDisplay => $"{PercentOfRoot:F1} %";

    public DateTime LastWriteUtc => Model.LastWriteUtc;

    public string LastWriteDisplay => Model.LastWriteUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public string? OwnerName => Model.OwnerName;

    public bool HasChildren => Model.Children.Count > 0;

    public ObservableCollection<FileSystemNodeViewModel> Children =>
        _children ??= new ObservableCollection<FileSystemNodeViewModel>(
            Model.Children.Select(child => new FileSystemNodeViewModel(child, _rootSizeInBytes, this)));

    /// <summary>Retire ce nœud de l'arbre (ViewModel + modèle), après une suppression ou un déplacement réussi.</summary>
    public void RemoveFromTree()
    {
        Parent?.Children.Remove(this);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void RaiseSizeChanged()
    {
        OnPropertyChanged(nameof(SizeInBytes));
        OnPropertyChanged(nameof(SizeDisplay));
        OnPropertyChanged(nameof(PercentOfRoot));
        OnPropertyChanged(nameof(PercentDisplay));
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "o", "Ko", "Mo", "Go", "To" };
        double size = bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:F2} {units[unitIndex]}";
    }
}
