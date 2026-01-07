using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CheckDuplicate.ViewModels;

namespace CheckDuplicate;

public class ViewLocator : IDataTemplate
{

    public Control? Build(object? data)
    {
        if (data is null)
            return null;
        
        return data switch
        {
            ViewModels.MainWindowViewModel => new Views.MainWindow(),
            ViewModels.HomeViewModel => new Views.HomeView(),
            ViewModels.ResultsViewModel => new Views.ResultsView(),
            ViewModels.HistoryViewModel => new Views.HistoryView(),
            ViewModels.AboutViewModel => new Views.AboutView(),
            ViewModels.ScanOptionsViewModel => new Views.ScanOptionsWindow(), // Technically a window, but ViewLocator might be used if embedded
            _ => new TextBlock { Text = "Not Found: " + data.GetType().Name }
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
