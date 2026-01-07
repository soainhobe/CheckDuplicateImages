using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CheckDuplicate.ViewModels;
using System;

namespace CheckDuplicate.Views;

public partial class ScanOptionsWindow : Window
{
    public ScanOptionsWindow()
    {
        InitializeComponent();
    }

    private async void AddFolder_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Folder to Scan",
            AllowMultiple = false // Single selection as we clear old list
        });

        if (folders.Count > 0 && DataContext is ScanOptionsViewModel vm)
        {
            vm.AddFolder(folders[0].Path.LocalPath);
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ScanOptionsViewModel vm)
        {
            vm.RequestClose += result => Close(result);
        }
    }
}
