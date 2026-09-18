using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AgySize.App.ViewModels;
using AgySize.App.Views;
using AgySize.Core.Reporting;

namespace AgySize.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Treemap.NodeClicked += (_, node) => ViewModel.NavigateTreemap(node);
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext!;

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Sélectionnez le dossier à analyser",
            AllowMultiple = false,
        });

        if (folders.Count > 0)
        {
            ViewModel.RootPath = folders[0].Path.LocalPath;
        }
    }

    private void TreemapUp_Click(object? sender, RoutedEventArgs e) => ViewModel.TreemapGoUp();

    private async void ExportCsv_Click(object? sender, RoutedEventArgs e)
    {
        var result = ViewModel.LastResult;
        if (result is null)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exporter la répartition des tailles en CSV",
            SuggestedFileName = "agysize-tailles.csv",
            FileTypeChoices = new[] { new FilePickerFileType("Fichier CSV") { Patterns = new[] { "*.csv" } } },
        });

        if (file is not null)
        {
            CsvReportWriter.WriteSizeReport(result, file.Path.LocalPath);
            ViewModel.StatusText = $"Export CSV enregistré : {file.Path.LocalPath}";
        }
    }

    private async void ExportHtml_Click(object? sender, RoutedEventArgs e)
    {
        var result = ViewModel.LastResult;
        if (result is null)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exporter le rapport HTML",
            SuggestedFileName = "agysize-rapport.html",
            FileTypeChoices = new[] { new FilePickerFileType("Rapport HTML") { Patterns = new[] { "*.html" } } },
        });

        if (file is not null)
        {
            HtmlReportWriter.WriteReport(result, file.Path.LocalPath);
            ViewModel.StatusText = $"Rapport HTML enregistré : {file.Path.LocalPath}";
        }
    }

    /// <summary>
    /// Sélection actuelle de l'arborescence, réduite aux éléments "racine" de la sélection : si un
    /// dossier et l'un de ses descendants sont sélectionnés ensemble, seul le dossier est conservé
    /// (le supprimer/déplacer traite déjà tout son contenu).
    /// </summary>
    private List<FileSystemNodeViewModel> GetTopLevelSelection()
    {
        var selected = new HashSet<FileSystemNodeViewModel>(Tree.SelectedItems.Cast<FileSystemNodeViewModel>());

        return selected.Where(node =>
        {
            var parent = node.Parent;
            while (parent is not null)
            {
                if (selected.Contains(parent))
                {
                    return false;
                }

                parent = parent.Parent;
            }

            return true;
        }).ToList();
    }

    private async void DeleteSelected_Click(object? sender, RoutedEventArgs e)
    {
        var nodes = GetTopLevelSelection();
        if (nodes.Count == 0)
        {
            ViewModel.StatusText = "Sélectionnez d'abord un ou plusieurs éléments dans l'arborescence.";
            return;
        }

        var message = nodes.Count == 1
            ? $"Supprimer « {nodes[0].RelativePath} » ({nodes[0].SizeDisplay}) ? " +
              "L'élément sera envoyé à la corbeille si possible."
            : $"Supprimer les {nodes.Count} éléments sélectionnés ? " +
              "Ils seront envoyés à la corbeille si possible.";

        var confirmed = await ConfirmDialog.ShowAsync(this, "Confirmer la suppression", message);

        if (confirmed)
        {
            foreach (var node in nodes)
            {
                ViewModel.ApplyDelete(node, useRecycleBin: true);
            }
        }
    }

    private async void MoveSelected_Click(object? sender, RoutedEventArgs e)
    {
        var nodes = GetTopLevelSelection();
        if (nodes.Count == 0)
        {
            ViewModel.StatusText = "Sélectionnez d'abord un ou plusieurs éléments dans l'arborescence.";
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choisissez le dossier de destination",
            AllowMultiple = false,
        });

        if (folders.Count > 0)
        {
            var destination = folders[0].Path.LocalPath;
            foreach (var node in nodes)
            {
                ViewModel.ApplyMove(node, destination);
            }
        }
    }

    private async void ExportLogs_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exporter le journal",
            SuggestedFileName = "agysize-journal.txt",
            FileTypeChoices = new[] { new FilePickerFileType("Fichier texte") { Patterns = new[] { "*.txt" } } },
        });

        if (file is null)
        {
            return;
        }

        var lines = ViewModel.LogRows.Select(r => $"{r.Time} [{r.Level}] {r.Source} - {r.Message}");
        await File.WriteAllLinesAsync(file.Path.LocalPath, lines);
        ViewModel.StatusText = $"Journal exporté : {file.Path.LocalPath}";
    }

    private void OpenLogsFolder_Click(object? sender, RoutedEventArgs e)
    {
        var logFilePath = ViewModel.LogFilePath;
        if (string.IsNullOrEmpty(logFilePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(logFilePath);
        if (directory is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
        }
        catch (Win32Exception ex)
        {
            ViewModel.StatusText = $"Impossible d'ouvrir le dossier des logs : {ex.Message}";
        }
    }
}
