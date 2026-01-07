using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace CheckDuplicate.ViewModels;

public partial class FolderItem : ObservableObject
{
    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (SetProperty(ref _isChecked, value))
            {
                UpdateChildren(value);
                UpdateParent();
            }
        }
    }

    // Determine if the change comes from internal cascading to prevent loops
    private bool _isCascading;

    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _path = string.Empty;

    public FolderItem? Parent { get; set; }

    public ObservableCollection<FolderItem> Children { get; } = new();

    public FolderItem() { }

    public FolderItem(string path, FolderItem? parent = null)
    {
        Path = path;
        Parent = parent;
        Name = System.IO.Path.GetFileName(path);
        if (string.IsNullOrEmpty(Name)) Name = path;
    }

    public void LoadChildren()
    {
        try
        {
            var options = new System.IO.EnumerationOptions { IgnoreInaccessible = true };
            var dirs = System.IO.Directory.GetDirectories(Path, "*", options);
            Children.Clear();
            foreach (var dir in dirs)
            {
                // Default subfolders to checked if parent is checked?
                // Or true by default as requested originally? 
                // Since parent is added as Checked usually, children should inherit.
                var child = new FolderItem(dir, this) { IsChecked = this.IsChecked };
                Children.Add(child);
            }
        }
        catch { /* Ignore access errors */ }
    }

    private void UpdateChildren(bool isChecked)
    {
        if (_isCascading) return;
        _isCascading = true;
        foreach (var child in Children)
        {
            child.SetIsCheckedInternal(isChecked);
        }
        _isCascading = false;
    }

    private void UpdateParent()
    {
        if (_isCascading || Parent == null) return;
        
        // Bubbling: If any child is unchecked -> Parent is unchecked.
        // If ALL children checked -> Parent is checked.
        // (Simplified logic: Parent state reflects "All Children Selected")
        bool allChecked = true;
        foreach (var child in Parent.Children)
        {
            if (!child.IsChecked)
            {
                allChecked = false;
                break;
            }
        }

        Parent.SetIsCheckedInternal(allChecked);
        // Parent should bubble up too
        Parent.UpdateParent(); 
    }

    public void SetIsCheckedInternal(bool value)
    {
        if (_isChecked != value)
        {
            _isCascading = true;
            SetProperty(ref _isChecked, value, nameof(IsChecked));
            UpdateChildren(value); // Propagate down
            _isCascading = false;
        }
    }
}

public partial class ScanOptionsViewModel : ViewModelBase
{
    // ... [Properties Omitted to match existing partial context if needed, but here I am creating the class content to replace]
    // Wait, the tool replaces lines. I need to be careful not to delete properties I don't show.
    // I will target the class blocks.

    // ...
}

public partial class ScanOptionsViewModel : ViewModelBase
{
    public ObservableCollection<FolderItem> Folders { get; } = new();
    
    public event Action<bool>? RequestClose;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAdvancedImageScanAvailable))]
    private bool _checkImages = true;
    
    [ObservableProperty]
    private bool _checkVideos; // Default false
    
    [ObservableProperty]
    private bool _checkDocuments; // Default false
    
    [ObservableProperty]
    private bool _checkMusic;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAdvancedImageScanAvailable))]
    private bool _checkOther;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAdvancedImageScanAvailable))]
    private string _otherTypes = string.Empty;

    [ObservableProperty]
    private FolderItem? _selectedFolder;

    [ObservableProperty]
    private bool _useContentComparison;

    [ObservableProperty]
    private bool _useNameAndSize;

    [ObservableProperty]
    private bool _useAutoComparison = true; // Default to Auto

    [ObservableProperty]
    private bool _useAdvancedImageComparison;

    [ObservableProperty]
    private int _comparisonStrengthIndex = 1; // Default to 1 (Loose)

    public bool IsAdvancedImageScanAvailable
    {
        get
        {
            if (CheckImages) return true;
            if (CheckOther && !string.IsNullOrWhiteSpace(OtherTypes))
            {
                var input = OtherTypes.ToLower();
                var imageKeywords = new[] { "jpg", "jpeg", "png", "bmp", "gif", "webp", "tiff", "heic", "raw" };
                // Simple check: if input contains any image extension
                return System.Linq.Enumerable.Any(imageKeywords, ext => input.Contains(ext));
            }
            return false;
        }
    }

    public ScanOptionsViewModel()
    {
        // Initial empty state or default
    }

    [RelayCommand]
    private void RemoveFolder()
    {
        if (SelectedFolder != null)
        {
            Folders.Remove(SelectedFolder);
        }
    }

    public void AddFolder(string path)
    {
        Folders.Clear(); // Clear old logic as requested
        var root = new FolderItem(path) { IsChecked = true };
        root.LoadChildren(); // Load immediate children for tree view
        Folders.Add(root);
    }

    [RelayCommand]
    private void Save()
    {
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    public CheckDuplicate.Models.ScanConfiguration GetConfiguration()
    {
        var config = new CheckDuplicate.Models.ScanConfiguration
        {
            IncludeImages = CheckImages,
            IncludeVideos = CheckVideos,
            IncludeDocuments = CheckDocuments,
            IncludeMusic = CheckMusic,
            IncludeOther = CheckOther,
            OtherExtensions = OtherTypes,
            Strength = (CheckDuplicate.Models.ComparisonStrength)ComparisonStrengthIndex
        };

        // Folders (Checked items in tree)
        var paths = new System.Collections.Generic.List<string>();
        CollectCheckedPaths(Folders, paths);
        config.SearchPaths = paths;

        // Method
        if (UseNameAndSize) config.Method = CheckDuplicate.Models.ComparisonMethod.NameAndSize;
        else if (UseContentComparison) config.Method = CheckDuplicate.Models.ComparisonMethod.Content;
        else if (UseAdvancedImageComparison) config.Method = CheckDuplicate.Models.ComparisonMethod.AdvancedImageScan;
        else config.Method = CheckDuplicate.Models.ComparisonMethod.Auto;

        return config;
    }

    private void CollectCheckedPaths(ObservableCollection<FolderItem> items, System.Collections.Generic.List<string> paths)
    {
        foreach (var item in items)
        {
            if (item.IsChecked)
            {
                paths.Add(item.Path);
                // Optimization: If parent is checked, we assume we scan everything inside.
                // Stopping recursion here prevents adding children redundantly.
                // Also, since 'IsChecked' on parent implies ALL children checked (due to bubbling),
                // we don't need to add specific children.
                // However, if we allow 'Loose' parents (files only), we might need different logic.
                // But with our strict bubbling, Checked Parent == Fully Checked Subtree.
                continue; 
            }
            
            // If not checked (or partial/indeterminate in future), check children.
            // Since we use strict bool, 'False' could mean 'Partial' (if some child is true).
            CollectCheckedPaths(item.Children, paths);
        }
    }
}
