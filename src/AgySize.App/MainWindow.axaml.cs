using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AgySize.App.ViewModels;
using AgySize.App.Views;
using AgySize.Core.Models;
using AgySize.Core.Reporting;

namespace AgySize.App;

public partial class MainWindow : Window
{
    /// <summary>
    /// Élément visé par le menu contextuel actuellement ouvert : un chemin complet résolu (pour
    /// "Ouvrir"/"Ouvrir l'emplacement"/"Copier le chemin", disponibles partout) et, quand la ligne
    /// correspond à un nœud réel de l'arborescence scannée, le nœud lui-même (nécessaire pour
    /// "Supprimer"/"Déplacer", qui mettent à jour les tailles agrégées).
    /// </summary>
    private sealed record ContextTarget(string FullPath, FileSystemNodeViewModel? Node);

    private Func<List<ContextTarget>> _contextSelectionProvider = () => new();

    public MainWindow()
    {
        InitializeComponent();
        Treemap.NodeClicked += (_, node) => ViewModel.NavigateTreemap(node);

        Tree.ContextRequested += (_, _) => _contextSelectionProvider = () =>
            GetTopLevelSelection().Select(n => new ContextTarget(n.Model.FullPath, n)).ToList();

        LargestFolderGrid.ContextRequested += (_, _) => _contextSelectionProvider = () =>
            LargestFolderGrid.SelectedItems.Cast<LargestItemRow>()
                .Select(r => new ContextTarget(r.Node.Model.FullPath, r.Node)).ToList();

        LargestFileGrid.ContextRequested += (_, _) => _contextSelectionProvider = () =>
            LargestFileGrid.SelectedItems.Cast<LargestItemRow>()
                .Select(r => new ContextTarget(r.Node.Model.FullPath, r.Node)).ToList();

        OldFileGrid.ContextRequested += (_, _) => _contextSelectionProvider = () =>
            OldFileGrid.SelectedItems.Cast<OldFileRow>()
                .Select(r => new ContextTarget(r.Node.Model.FullPath, r.Node)).ToList();

        EmptyFolderGrid.ContextRequested += (_, _) => _contextSelectionProvider = () =>
            EmptyFolderGrid.SelectedItems.Cast<EmptyFolderRow>()
                .Select(r => new ContextTarget(r.Node.Model.FullPath, r.Node)).ToList();

        DuplicateGrid.ContextRequested += (_, _) => _contextSelectionProvider = () =>
            DuplicateGrid.SelectedItems.Cast<DuplicateRow>()
                .Select(r => new ContextTarget(r.Node.Model.FullPath, r.Node)).ToList();

        AuditGrid.ContextRequested += (_, _) => _contextSelectionProvider = () =>
            AuditGrid.SelectedItems.Cast<AuditIssueRow>()
                .Select(i => new ContextTarget(i.Node?.FullPath ?? ResolveFullPath(i.RelativePath), null)).ToList();

        PermissionsGrid.ContextRequested += (_, _) => _contextSelectionProvider = () =>
            PermissionsGrid.SelectedItems.Cast<PermissionFinding>()
                .Select(f => new ContextTarget(ResolveFullPath(f.RelativePath), null)).ToList();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext!;

    /// <summary>Reconstitue le chemin complet d'une ligne qui ne référence qu'un chemin relatif (audit, droits).</summary>
    private string ResolveFullPath(string relativePath)
    {
        var root = ViewModel.RootPath;
        if (string.IsNullOrWhiteSpace(root))
        {
            return relativePath;
        }

        return relativePath.Length == 0 || relativePath == "(racine)"
            ? root
            : Path.Combine(root, relativePath);
    }

    private void OpenPath(string fullPath)
    {
        try
        {
            if (Directory.Exists(fullPath) || File.Exists(fullPath))
            {
                Process.Start(new ProcessStartInfo { FileName = fullPath, UseShellExecute = true });
            }
            else
            {
                ViewModel.StatusText = $"Introuvable sur le disque : {fullPath}";
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            ViewModel.StatusText = $"Impossible d'ouvrir : {ex.Message}";
        }
    }

    private void OpenContainingFolder(string fullPath)
    {
        try
        {
            if (OperatingSystem.IsWindows() && File.Exists(fullPath))
            {
                var psi = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
                psi.ArgumentList.Add($"/select,{fullPath}");
                Process.Start(psi);
                return;
            }

            var directory = Directory.Exists(fullPath) ? fullPath : Path.GetDirectoryName(fullPath);
            if (directory is null || !Directory.Exists(directory))
            {
                ViewModel.StatusText = $"Dossier introuvable pour : {fullPath}";
                return;
            }

            Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            ViewModel.StatusText = $"Impossible d'ouvrir l'emplacement : {ex.Message}";
        }
    }

    private void ContextOpen_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var target in _contextSelectionProvider())
        {
            OpenPath(target.FullPath);
        }
    }

    private void ContextOpenLocation_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var target in _contextSelectionProvider())
        {
            OpenContainingFolder(target.FullPath);
        }
    }

    private async void ContextCopyPath_Click(object? sender, RoutedEventArgs e)
    {
        var targets = _contextSelectionProvider();
        if (targets.Count == 0)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        var clipboard = topLevel?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        var text = string.Join(Environment.NewLine, targets.Select(t => t.FullPath));
        await clipboard.SetTextAsync(text);
        ViewModel.StatusText = targets.Count == 1 ? $"Chemin copié : {text}" : $"{targets.Count} chemins copiés.";
    }

    private async void ContextDelete_Click(object? sender, RoutedEventArgs e) =>
        await DeleteNodesAsync(_contextSelectionProvider().Where(t => t.Node is not null).Select(t => t.Node!).ToList());

    private async void ContextMove_Click(object? sender, RoutedEventArgs e) =>
        await MoveNodesAsync(_contextSelectionProvider().Where(t => t.Node is not null).Select(t => t.Node!).ToList());

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

    /// <summary>
    /// Dossier proposé par défaut dans les sélecteurs d'enregistrement. Sans ça, Avalonia peut
    /// proposer le dossier de travail courant du processus — qui, une fois l'application installée
    /// via le .msi, est le dossier d'installation sous "Program Files", protégé en écriture pour un
    /// utilisateur standard (voir le plantage "Access to the path 'C:\Program Files\...' is denied").
    /// </summary>
    private static async Task<IStorageFolder?> GetDefaultExportFolderAsync(TopLevel topLevel)
    {
        try
        {
            return await topLevel.StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Documents);
        }
        catch (Exception)
        {
            return null;
        }
    }

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
            SuggestedStartLocation = await GetDefaultExportFolderAsync(topLevel),
            FileTypeChoices = new[] { new FilePickerFileType("Fichier CSV") { Patterns = new[] { "*.csv" } } },
        });

        if (file is null)
        {
            return;
        }

        try
        {
            CsvReportWriter.WriteSizeReport(result, file.Path.LocalPath);
            ViewModel.StatusText = $"Export CSV enregistré : {file.Path.LocalPath}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ViewModel.StatusText = $"Export impossible : {ex.Message}";
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
            SuggestedStartLocation = await GetDefaultExportFolderAsync(topLevel),
            FileTypeChoices = new[] { new FilePickerFileType("Rapport HTML") { Patterns = new[] { "*.html" } } },
        });

        if (file is null)
        {
            return;
        }

        try
        {
            HtmlReportWriter.WriteReport(result, file.Path.LocalPath);
            ViewModel.StatusText = $"Rapport HTML enregistré : {file.Path.LocalPath}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ViewModel.StatusText = $"Export impossible : {ex.Message}";
        }
    }

    private async void ExportAuditCsv_Click(object? sender, RoutedEventArgs e)
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
            Title = "Exporter les anomalies SharePoint en CSV",
            SuggestedFileName = "agysize-anomalies-sharepoint.csv",
            SuggestedStartLocation = await GetDefaultExportFolderAsync(topLevel),
            FileTypeChoices = new[] { new FilePickerFileType("Fichier CSV") { Patterns = new[] { "*.csv" } } },
        });

        if (file is null)
        {
            return;
        }

        try
        {
            CsvReportWriter.WriteAuditIssues(result, file.Path.LocalPath);
            ViewModel.StatusText = $"Export des anomalies CSV enregistré : {file.Path.LocalPath}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ViewModel.StatusText = $"Export impossible : {ex.Message}";
        }
    }

    private async void ExportAuditPdf_Click(object? sender, RoutedEventArgs e)
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
            Title = "Exporter le rapport PDF client (audit SharePoint)",
            SuggestedFileName = "agysize-rapport-audit-client.pdf",
            SuggestedStartLocation = await GetDefaultExportFolderAsync(topLevel),
            FileTypeChoices = new[] { new FilePickerFileType("Rapport PDF") { Patterns = new[] { "*.pdf" } } },
        });

        if (file is null)
        {
            return;
        }

        try
        {
            PdfReportWriter.WriteClientReport(result, file.Path.LocalPath);
            ViewModel.StatusText = $"Rapport PDF client enregistré : {file.Path.LocalPath}";
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            ViewModel.StatusText = $"Impossible de générer le PDF : {ex.Message}";
        }
    }

    private async void AutoFixVisible_Click(object? sender, RoutedEventArgs e) =>
        await RunAutoFix(ViewModel.FilteredAuditIssueRows.Where(r => r.AutoFixable).ToList());

    private async void AuditContextAutoFix_Click(object? sender, RoutedEventArgs e) =>
        await RunAutoFix(AuditGrid.SelectedItems.Cast<AuditIssueRow>().Where(r => r.AutoFixable).ToList());

    private async Task RunAutoFix(List<AuditIssueRow> rows)
    {
        var distinctPaths = rows.Select(r => r.RelativePath).Distinct().Count();
        if (distinctPaths == 0)
        {
            ViewModel.StatusText = "Aucun élément corrigible dans la sélection.";
            return;
        }

        var message = distinctPaths == 1
            ? $"Corriger automatiquement « {rows[0].RelativePath} » ? Il sera renommé en « {rows[0].SuggestedName} »."
            : $"Corriger automatiquement {distinctPaths} élément(s) (renommage) ?";

        var confirmed = await ConfirmDialog.ShowAsync(this, "Confirmer la correction automatique", message);
        if (confirmed)
        {
            ViewModel.AutoFixRows(rows);
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

    private async void DeleteSelected_Click(object? sender, RoutedEventArgs e) => await DeleteNodesAsync(GetTopLevelSelection());

    private async void MoveSelected_Click(object? sender, RoutedEventArgs e) => await MoveNodesAsync(GetTopLevelSelection());

    private async Task DeleteNodesAsync(List<FileSystemNodeViewModel> nodes)
    {
        if (nodes.Count == 0)
        {
            ViewModel.StatusText = "Sélectionnez d'abord un ou plusieurs éléments.";
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

    private async Task MoveNodesAsync(List<FileSystemNodeViewModel> nodes)
    {
        if (nodes.Count == 0)
        {
            ViewModel.StatusText = "Sélectionnez d'abord un ou plusieurs éléments.";
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
            SuggestedStartLocation = await GetDefaultExportFolderAsync(topLevel),
            FileTypeChoices = new[] { new FilePickerFileType("Fichier texte") { Patterns = new[] { "*.txt" } } },
        });

        if (file is null)
        {
            return;
        }

        try
        {
            var lines = ViewModel.LogRows.Select(r => $"{r.Time} [{r.Level}] {r.Source} - {r.Message}");
            await File.WriteAllLinesAsync(file.Path.LocalPath, lines);
            ViewModel.StatusText = $"Journal exporté : {file.Path.LocalPath}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ViewModel.StatusText = $"Export impossible : {ex.Message}";
        }
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
