using System;
using System.Linq;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CheckDuplicate.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Collections;

namespace CheckDuplicate.ViewModels;

public partial class ResultsViewModel : ViewModelBase
{
    private readonly Services.IDuplicateCheckerService _duplicateCheckerService;
    private readonly Services.IFileService _fileService;
    private readonly Services.IHistoryService _historyService;
    private ScanOptionsViewModel _scanOptionsViewModel;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartNewScanCommand))]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusMessage = "Ready to scan.";

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _spaceSavedText = "Space saved: 0 B";

    private List<DuplicateFileItem> _currentResults = new();

    public DataGridCollectionView ItemsView { get; set; }

    // Design-time or fallback
    public ResultsViewModel() 
    {
         _duplicateCheckerService = null!;
         _fileService = null!;
         _historyService = null!;
         ItemsView = null!;
         _scanOptionsViewModel = new ScanOptionsViewModel();
    }

    public ResultsViewModel(Services.IDuplicateCheckerService duplicateCheckerService, Services.IFileService fileService, Services.IHistoryService historyService)
    {
        _duplicateCheckerService = duplicateCheckerService;
        _fileService = fileService;
        _historyService = historyService;
        _scanOptionsViewModel = new ScanOptionsViewModel(); // Initialize with defaults
        
        // Listen to folder changes to update Command state
        _scanOptionsViewModel.Folders.CollectionChanged += (s, e) => StartNewScanCommand.NotifyCanExecuteChanged();

        // Initial Empty State
        ItemsView = new DataGridCollectionView(new List<DuplicateFileItem>());
        ItemsView.GroupDescriptions.Add(new DataGridPathGroupDescription("Category"));
        ItemsView.GroupDescriptions.Add(new DataGridPathGroupDescription("DetailsGroup"));
        // Sort: Category (Duplicate < Single), then Details, then Name
        ItemsView.SortDescriptions.Add(DataGridSortDescription.FromPath("Category", System.ComponentModel.ListSortDirection.Ascending));
        ItemsView.SortDescriptions.Add(DataGridSortDescription.FromPath("DetailsGroup", System.ComponentModel.ListSortDirection.Ascending));
        ItemsView.SortDescriptions.Add(DataGridSortDescription.FromPath("FileName", System.ComponentModel.ListSortDirection.Ascending));
    }

    private bool CanStartNewScan => !IsScanning && _scanOptionsViewModel.Folders.Count > 0;

    [RelayCommand(CanExecute = nameof(CanStartNewScan))]
    private async Task StartNewScan()
    {
        if (IsScanning) return;

        try
        {
            IsScanning = true;
            ProgressValue = 0;
            StatusMessage = "Scanning...";
            
            var config = _scanOptionsViewModel.GetConfiguration();
            
            if (!System.Linq.Enumerable.Any(config.SearchPaths))
            {
                 StatusMessage = "No folders selected.";
                 IsScanning = false;
                 return;
            }

            var progress = new Progress<double>(p => 
            {
                ProgressValue = p;
                StatusMessage = $"Scanning... {p:0}%";
            });

            var rawResults = await _duplicateCheckerService.ScanAsync(config, progress);
            
            // Filter: Only Duplicates
            _currentResults = rawResults
                .Where(x => x.Category == "Duplicate File")
                .ToList();
            
            CalculateSpaceSaved();

            // Update UI
            ItemsView = new DataGridCollectionView(_currentResults);
            ItemsView.GroupDescriptions.Add(new DataGridPathGroupDescription("DetailsGroup")); // Removed "Category" grouping as we only have duplicates
            
            // Apply Sort
            // Sort by Group (DetailsGroup) then by Name
            ItemsView.SortDescriptions.Add(DataGridSortDescription.FromPath("DetailsGroup", System.ComponentModel.ListSortDirection.Ascending));
            ItemsView.SortDescriptions.Add(DataGridSortDescription.FromPath("FileName", System.ComponentModel.ListSortDirection.Ascending));
            
            OnPropertyChanged(nameof(ItemsView));

            StatusMessage = $"Scan complete. Found {_currentResults.Count} duplicates.";
            ProgressValue = 100;
            ExportReportCommand.NotifyCanExecuteChanged();

            // Auto-Save History
            long totalSavedBytes = CalculateTotalSavedBytes();
            var session = new HistorySession
            {
                 TotalDuplicates = _currentResults.Count,
                 TotalSizeSaved = totalSavedBytes,
                 Items = new List<DuplicateFileItem>(_currentResults)
            };
            // Fire and forget or await? Safe to await here.
            await _historyService.SaveSessionAsync(session);
            StatusMessage += " (Saved to History)";
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void CalculateSpaceSaved()
    {
        long totalSavedBytes = CalculateTotalSavedBytes();
        SpaceSavedText = $"Space saved: {FormatSize(totalSavedBytes)}";
    }

    private long CalculateTotalSavedBytes()
    {
        long totalSavedBytes = 0;
        var groups = _currentResults.GroupBy(x => x.DetailsGroup);
        foreach(var g in groups)
        {
           int count = g.Count();
           if (count > 1) 
           {
               totalSavedBytes += (count - 1) * g.First().SizeBytes;
           }
        }
        return totalSavedBytes;
    }

    private string FormatSize(double len)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.0} {sizes[order]}";
    }

    [RelayCommand]
    private void OpenScanOptions()
    {
         var window = new CheckDuplicate.Views.ScanOptionsWindow();
         window.DataContext = _scanOptionsViewModel;
         
         if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
         {
             window.ShowDialog(desktop.MainWindow);
         }
    }
    
    [RelayCommand]
    private void OpenFolder(DuplicateFileItem? item)
    {
        if (item == null) return;
        _fileService.OpenFolder(item.FullPath);
    }

    [RelayCommand]
    private void DeleteFile(DuplicateFileItem? item)
    {
        if (item == null) return;
        
        try 
        {
            _fileService.DeleteToRecycleBin(item.FullPath);
            
            // Remove from internal list
            _currentResults.Remove(item);
            
            // Check if group still valid
            // Find other items in same group
            var groupItems = _currentResults.Where(x => x.DetailsGroup == item.DetailsGroup).ToList();
            if (groupItems.Count == 1)
            {
                // Only 1 item left, no longer a duplicate. Remove it properly.
                _currentResults.Remove(groupItems[0]);
            }
            
            // Re-calculate space
            CalculateSpaceSaved();
            
            // Refresh View
            // Creating new view is easiest way to ensure groups update correctly
            // and filters applied.
            // Preserving sort order
            var newView = new DataGridCollectionView(_currentResults);
            newView.GroupDescriptions.Add(new DataGridPathGroupDescription("DetailsGroup"));
            newView.SortDescriptions.Add(DataGridSortDescription.FromPath("DetailsGroup", System.ComponentModel.ListSortDirection.Ascending));
            newView.SortDescriptions.Add(DataGridSortDescription.FromPath("FileName", System.ComponentModel.ListSortDirection.Ascending));
            
            ItemsView = newView;
            OnPropertyChanged(nameof(ItemsView));

            StatusMessage = $"Deleted: {item.FileName}";
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"Delete Error: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportReport()
    {
        if (_currentResults.Count == 0) return;

        var lifetime = Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
        if (lifetime?.MainWindow == null) return;

        try
        {
            var storageProvider = lifetime.MainWindow.StorageProvider;
            var file = await storageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Export Duplicate Report",
                DefaultExtension = "csv",
                SuggestedFileName = $"DuplicateReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                FileTypeChoices = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } }
                }
            });

            if (file != null)
            {
                using var stream = await file.OpenWriteAsync();
                using var writer = new System.IO.StreamWriter(stream);
                
                await writer.WriteLineAsync("Group/File,Path,Size,Created Date");

                var groups = _currentResults.GroupBy(x => x.DetailsGroup);
                foreach (var group in groups)
                {
                    // Group Header
                    await writer.WriteLineAsync($"\"[GROUP] {group.Key}\",,,");
                    
                    foreach (var item in group)
                    {
                        // Item Row (Indented conceptually by being in second column or just explicitly structure)
                        // User asked for "Tree structure", usually implies visual hierarchy.
                        // Standard CSV tree:
                        // Column 1 is Tree Node Name. Column 2 is attributes.
                        // Or Column 1 is Group, Col 2 is File.
                        
                        // Implementation:
                        // Col 1: Empty (to show indentation under group) or "  " + FileName
                        // Col 2: Full Path
                        await writer.WriteLineAsync($",\"{item.FullPath}\",\"{item.Size}\",\"{item.CreatedDate}\"");
                    }
                    // Blank line separator
                    await writer.WriteLineAsync(",,,");
                }

                StatusMessage = "Report exported successfully.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export Failed: {ex.Message}";
        }
    }

    private bool CanExport => _currentResults.Count > 0;

    [RelayCommand]
    private void DeleteAllDuplicates() { }
}
