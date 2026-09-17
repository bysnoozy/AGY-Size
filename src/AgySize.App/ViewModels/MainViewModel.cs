using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AgySize.Core.Logging;
using AgySize.Core.Models;
using AgySize.Core.Operations;
using AgySize.Core.Scanning;
using AgySize.Core.Scheduling;
using AgySize.Core.Startup;
using Avalonia.Threading;

namespace AgySize.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly FileSystemScanner _scanner = new();
    private readonly InMemoryAuditLogger _memoryLogger = new();
    private readonly FileAuditLogger? _fileLogger;
    private readonly IAuditLogger _logger;

    private CancellationTokenSource? _cancellationTokenSource;

    private string _rootPath = string.Empty;
    private string _statusText = "Sélectionnez un dossier à analyser.";
    private string _progressText = string.Empty;
    private bool _isScanning;
    private bool _isFindingDuplicates;
    private bool _analyzePermissions;
    private bool _autoStartEnabled;
    private string _scheduleTime = "02:00";
    private string _totalFiles = "-";
    private string _totalFolders = "-";
    private string _totalSize = "-";
    private string _emptyFolderCount = "-";
    private string _permissionFindingCount = "-";
    private string _duplicateSummary = "-";
    private ScanResult? _lastResult;
    private FileSystemNodeViewModel? _selectedNode;
    private FileSystemNodeViewModel? _treemapNode;
    private string _treemapPath = "(racine)";

    public MainViewModel()
    {
        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AGY-Size", "logs");
            _fileLogger = new FileAuditLogger(Path.Combine(logDirectory, "agysize.log"));
            _logger = new CompositeAuditLogger(_memoryLogger, _fileLogger);
        }
        catch (IOException)
        {
            _logger = _memoryLogger;
        }
        catch (UnauthorizedAccessException)
        {
            _logger = _memoryLogger;
        }

        _memoryLogger.EntryLogged += (_, entry) => Dispatcher.UIThread.Post(() =>
        {
            LogRows.Add(new LogRow
            {
                Time = entry.TimestampUtc.ToLocalTime().ToString("HH:mm:ss"),
                Level = entry.Level.ToString(),
                Source = entry.Source,
                Message = entry.Message,
            });

            while (LogRows.Count > 2000)
            {
                LogRows.RemoveAt(0);
            }
        });

        if (OperatingSystem.IsWindows())
        {
            try
            {
                _autoStartEnabled = AutoStartManager.IsEnabled();
            }
            catch (Exception)
            {
                _autoStartEnabled = false;
            }
        }

        StartScanCommand = new RelayCommand(async _ => await StartScanAsync(), _ => !IsScanning && !string.IsNullOrWhiteSpace(RootPath));
        CancelScanCommand = new RelayCommand(_ => Cancel(), _ => IsScanning);
        FindDuplicatesCommand = new RelayCommand(async _ => await FindDuplicatesAsync(), _ => CanExport && !IsScanning && !IsFindingDuplicates);
        ScheduleDailyScanCommand = new RelayCommand(_ => ScheduleDailyScan(), _ => CanExport && OperatingSystem.IsWindows());

        _logger.Info("Application", "AGY-Size démarré.");
    }

    public ObservableCollection<FileSystemNodeViewModel> TreeRoots { get; } = new();

    public ObservableCollection<ExtensionRow> ExtensionRows { get; } = new();

    public ObservableCollection<LargestItemRow> LargestFolderRows { get; } = new();

    public ObservableCollection<LargestItemRow> LargestFileRows { get; } = new();

    public ObservableCollection<OldFileRow> OldFileRows { get; } = new();

    public ObservableCollection<EmptyFolderRow> EmptyFolderRows { get; } = new();

    public ObservableCollection<PermissionFinding> PermissionFindings { get; } = new();

    public ObservableCollection<DuplicateRow> DuplicateRows { get; } = new();

    public ObservableCollection<LogRow> LogRows { get; } = new();

    public RelayCommand StartScanCommand { get; }

    public RelayCommand CancelScanCommand { get; }

    public RelayCommand FindDuplicatesCommand { get; }

    public RelayCommand ScheduleDailyScanCommand { get; }

    public string LogFilePath => _fileLogger?.FilePath ?? string.Empty;

    public string RootPath
    {
        get => _rootPath;
        set
        {
            if (SetField(ref _rootPath, value))
            {
                StartScanCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Settable depuis le code-behind de la vue après une action (export, suppression...).</summary>
    public string StatusText
    {
        get => _statusText;
        internal set => SetField(ref _statusText, value);
    }

    public string ProgressText
    {
        get => _progressText;
        private set => SetField(ref _progressText, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetField(ref _isScanning, value))
            {
                StartScanCommand.RaiseCanExecuteChanged();
                CancelScanCommand.RaiseCanExecuteChanged();
                FindDuplicatesCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsFindingDuplicates
    {
        get => _isFindingDuplicates;
        private set
        {
            if (SetField(ref _isFindingDuplicates, value))
            {
                FindDuplicatesCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool AnalyzePermissions
    {
        get => _analyzePermissions;
        set => SetField(ref _analyzePermissions, value);
    }

    public bool AutoStartEnabled
    {
        get => _autoStartEnabled;
        set
        {
            if (!SetField(ref _autoStartEnabled, value))
            {
                return;
            }

            if (!OperatingSystem.IsWindows())
            {
                StatusText = "Le démarrage automatique n'est disponible que sous Windows.";
                return;
            }

            try
            {
                var executablePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
                AutoStartManager.SetEnabled(value, executablePath);
                StatusText = value ? "AGY-Size démarrera avec Windows." : "Démarrage automatique désactivé.";
                _logger.Info("Démarrage auto", StatusText);
            }
            catch (Exception ex)
            {
                StatusText = $"Impossible de modifier le démarrage automatique : {ex.Message}";
            }
        }
    }

    public string ScheduleTime
    {
        get => _scheduleTime;
        set => SetField(ref _scheduleTime, value);
    }

    public string TotalFiles
    {
        get => _totalFiles;
        private set => SetField(ref _totalFiles, value);
    }

    public string TotalFolders
    {
        get => _totalFolders;
        private set => SetField(ref _totalFolders, value);
    }

    public string TotalSize
    {
        get => _totalSize;
        private set => SetField(ref _totalSize, value);
    }

    public string EmptyFolderCount
    {
        get => _emptyFolderCount;
        private set => SetField(ref _emptyFolderCount, value);
    }

    public string PermissionFindingCount
    {
        get => _permissionFindingCount;
        private set => SetField(ref _permissionFindingCount, value);
    }

    public string DuplicateSummary
    {
        get => _duplicateSummary;
        private set => SetField(ref _duplicateSummary, value);
    }

    public FileSystemNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set => SetField(ref _selectedNode, value);
    }

    public FileSystemNodeViewModel? TreemapNode
    {
        get => _treemapNode;
        private set => SetField(ref _treemapNode, value);
    }

    public string TreemapPath
    {
        get => _treemapPath;
        private set => SetField(ref _treemapPath, value);
    }

    /// <summary>Lu par le code-behind de la vue pour alimenter les exports CSV/HTML.</summary>
    public ScanResult? LastResult
    {
        get => _lastResult;
        private set
        {
            if (SetField(ref _lastResult, value))
            {
                OnPropertyChanged(nameof(CanExport));
                FindDuplicatesCommand.RaiseCanExecuteChanged();
                ScheduleDailyScanCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanExport => LastResult is not null;

    private async Task StartScanAsync()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        IsScanning = true;
        StatusText = "Analyse en cours...";
        TreeRoots.Clear();
        ExtensionRows.Clear();
        LargestFolderRows.Clear();
        LargestFileRows.Clear();
        OldFileRows.Clear();
        EmptyFolderRows.Clear();
        PermissionFindings.Clear();
        DuplicateRows.Clear();
        DuplicateSummary = "-";
        LastResult = null;

        var options = new ScanOptions { RootPath = RootPath, AnalyzePermissions = AnalyzePermissions };
        var progress = new Progress<ScanProgress>(p =>
        {
            ProgressText = $"{p.FilesScanned:N0} fichiers, {p.FoldersScanned:N0} dossiers analysés";
        });

        try
        {
            var result = await Task.Run(
                () => _scanner.Scan(options, _logger, progress, _cancellationTokenSource.Token),
                _cancellationTokenSource.Token);

            ApplyResult(result, options.OldFileThresholdDays);

            StatusText = result.Errors.Count > 0
                ? $"Analyse terminée avec {result.Errors.Count} erreur(s) de lecture."
                : "Analyse terminée.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Analyse annulée.";
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or UnauthorizedAccessException or IOException)
        {
            StatusText = $"Erreur : {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            _cancellationTokenSource = null;
        }
    }

    private void ApplyResult(ScanResult result, int oldFileThresholdDays)
    {
        LastResult = result;

        var rootVm = new FileSystemNodeViewModel(result.RootNode, Math.Max(1, result.RootNode.SizeInBytes));
        TreeRoots.Add(rootVm);
        TreemapNode = rootVm;
        TreemapPath = "(racine)";

        TotalFiles = result.RootNode.FileCount.ToString("N0");
        TotalFolders = result.RootNode.FolderCount.ToString("N0");
        TotalSize = FormatBytes(result.RootNode.SizeInBytes);
        EmptyFolderCount = result.EmptyFolders.Count.ToString("N0");
        PermissionFindingCount = result.PermissionFindings.Count.ToString("N0");

        foreach (var stat in result.ExtensionStats)
        {
            ExtensionRows.Add(new ExtensionRow
            {
                Extension = stat.Extension,
                FileCount = stat.FileCount.ToString("N0"),
                Size = FormatBytes(stat.TotalSizeInBytes),
                SizeInBytes = stat.TotalSizeInBytes,
            });
        }

        foreach (var folder in result.LargestFolders)
        {
            LargestFolderRows.Add(new LargestItemRow
            {
                RelativePath = folder.RelativePath,
                Kind = "Dossier",
                Size = FormatBytes(folder.SizeInBytes),
                Node = new FileSystemNodeViewModel(folder, result.RootNode.SizeInBytes),
            });
        }

        foreach (var file in result.LargestFiles)
        {
            LargestFileRows.Add(new LargestItemRow
            {
                RelativePath = file.RelativePath,
                Kind = "Fichier",
                Size = FormatBytes(file.SizeInBytes),
                Node = new FileSystemNodeViewModel(file, result.RootNode.SizeInBytes),
            });
        }

        var oldThreshold = DateTime.UtcNow.AddDays(-oldFileThresholdDays);
        foreach (var file in result.RootNode.Files().Where(f => f.LastWriteUtc < oldThreshold).OrderBy(f => f.LastWriteUtc).Take(500))
        {
            OldFileRows.Add(new OldFileRow
            {
                RelativePath = file.RelativePath,
                LastModified = file.LastWriteUtc.ToLocalTime().ToString("yyyy-MM-dd"),
                Size = FormatBytes(file.SizeInBytes),
                Node = new FileSystemNodeViewModel(file, result.RootNode.SizeInBytes),
            });
        }

        foreach (var folder in result.EmptyFolders)
        {
            EmptyFolderRows.Add(new EmptyFolderRow
            {
                RelativePath = folder.RelativePath.Length == 0 ? "(racine)" : folder.RelativePath,
                Node = new FileSystemNodeViewModel(folder, result.RootNode.SizeInBytes),
            });
        }

        foreach (var finding in result.PermissionFindings)
        {
            PermissionFindings.Add(finding);
        }
    }

    private async Task FindDuplicatesAsync()
    {
        if (LastResult is null)
        {
            return;
        }

        IsFindingDuplicates = true;
        DuplicateRows.Clear();
        StatusText = "Recherche de doublons en cours...";

        try
        {
            var groups = await Task.Run(() => DuplicateFinder.FindDuplicates(LastResult.RootNode, _logger));

            var groupIndex = 0;
            long wastedTotal = 0;
            foreach (var group in groups)
            {
                groupIndex++;
                wastedTotal += group.WastedBytes;
                foreach (var file in group.Files)
                {
                    DuplicateRows.Add(new DuplicateRow
                    {
                        Groupe = $"#{groupIndex}",
                        RelativePath = file.RelativePath,
                        Size = FormatBytes(file.SizeInBytes),
                    });
                }
            }

            DuplicateSummary = groups.Count == 0
                ? "Aucun doublon trouvé."
                : $"{groups.Count:N0} groupe(s) de doublons, {FormatBytes(wastedTotal)} récupérables.";
            StatusText = "Recherche de doublons terminée.";
            _logger.Info("Doublons", DuplicateSummary);
        }
        finally
        {
            IsFindingDuplicates = false;
        }
    }

    public void NavigateTreemap(FileSystemNodeViewModel node)
    {
        if (node.Kind != FileSystemNodeKind.Folder)
        {
            return;
        }

        TreemapNode = node;
        TreemapPath = node.RelativePath.Length == 0 ? "(racine)" : node.RelativePath;
    }

    public void TreemapGoUp()
    {
        if (TreemapNode?.Parent is { } parent)
        {
            NavigateTreemap(parent);
        }
    }

    public void ApplyDelete(FileSystemNodeViewModel node, bool useRecycleBin)
    {
        try
        {
            FileOperations.Delete(node.Model, useRecycleBin);
            RaiseAncestorSizeChanges(node);
            node.RemoveFromTree();
            RefreshTotalsFromRoot();
            StatusText = $"Supprimé : {node.RelativePath}";
            _logger.Info("Suppression", $"'{node.Model.FullPath}' supprimé ({(useRecycleBin ? "corbeille" : "définitif")}).");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = $"Suppression impossible : {ex.Message}";
            _logger.Error("Suppression", $"Échec sur '{node.RelativePath}' : {ex.Message}");
        }
    }

    public void ApplyMove(FileSystemNodeViewModel node, string destinationDirectory)
    {
        try
        {
            FileOperations.Move(node.Model, destinationDirectory);
            RaiseAncestorSizeChanges(node);
            node.RemoveFromTree();
            RefreshTotalsFromRoot();
            StatusText = $"Déplacé vers : {destinationDirectory}";
            _logger.Info("Déplacement", $"'{node.RelativePath}' déplacé vers '{destinationDirectory}'.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = $"Déplacement impossible : {ex.Message}";
            _logger.Error("Déplacement", $"Échec sur '{node.RelativePath}' : {ex.Message}");
        }
    }

    private static void RaiseAncestorSizeChanges(FileSystemNodeViewModel node)
    {
        var ancestor = node.Parent;
        while (ancestor is not null)
        {
            ancestor.RaiseSizeChanged();
            ancestor = ancestor.Parent;
        }
    }

    private void RefreshTotalsFromRoot()
    {
        if (LastResult is null)
        {
            return;
        }

        TotalFiles = LastResult.RootNode.FileCount.ToString("N0");
        TotalFolders = LastResult.RootNode.FolderCount.ToString("N0");
        TotalSize = FormatBytes(LastResult.RootNode.SizeInBytes);
    }

    public void ScheduleDailyScan()
    {
        if (!OperatingSystem.IsWindows())
        {
            StatusText = "La planification de scans n'est disponible que sous Windows.";
            return;
        }

        if (string.IsNullOrWhiteSpace(RootPath))
        {
            StatusText = "Sélectionnez un dossier avant de planifier un scan.";
            return;
        }

        var cliPath = Path.Combine(AppContext.BaseDirectory, "AgySize.Cli.exe");
        if (!File.Exists(cliPath))
        {
            StatusText = "AgySize.Cli.exe est introuvable à côté de l'application ; publiez-le avant de planifier un scan.";
            return;
        }

        if (!TimeOnly.TryParse(ScheduleTime, out var time))
        {
            StatusText = "Heure de planification invalide (format attendu : HH:mm).";
            return;
        }

        try
        {
            var exportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AGY-Size", "rapports", "rapport-planifie.html");

            ScheduledScanManager.CreateDailyTask("AGY-Size - Scan quotidien", cliPath, RootPath, exportPath, time);
            StatusText = $"Scan quotidien planifié à {time:HH:mm} (tâche Windows 'AGY-Size - Scan quotidien').";
            _logger.Info("Planification", $"Tâche planifiée pour '{RootPath}' à {time:HH:mm}.");
        }
        catch (InvalidOperationException ex)
        {
            StatusText = $"Impossible de planifier le scan : {ex.Message}";
        }
    }

    private void Cancel()
    {
        _cancellationTokenSource?.Cancel();
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName!);
        return true;
    }
}
