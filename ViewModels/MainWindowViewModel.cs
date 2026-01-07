using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace CheckDuplicate.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private ViewModelBase _currentPage = null!;

    [ObservableProperty]
    private bool _isSidebarOpen = true;

    [ObservableProperty]
    private bool _isHomeActive;

    [ObservableProperty]
    private bool _isHistoryActive;

    [ObservableProperty]
    private bool _isAboutActive;

    public MainWindowViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        // Default to Home (Results)
        NavigateToHome();
    }

    /// <summary>
    /// Design-time constructor
    /// </summary>
    public MainWindowViewModel()
    {
        _serviceProvider = null!;
        _currentPage = new ResultsViewModel(null!, null!, null!, null!);
    }

    [RelayCommand]
    private void NavigateToHome()
    {
        CurrentPage = _serviceProvider.GetRequiredService<ResultsViewModel>();
        UpdateSelection(isHome: true);
    }

    [RelayCommand]
    private void NavigateToResults()
    {
        // Deprecated/Removed from UI, but kept logic if needed or redirect
        NavigateToHome();
    }

    [RelayCommand]
    private void NavigateToHistory()
    {
        CurrentPage = _serviceProvider.GetRequiredService<HistoryViewModel>();
        UpdateSelection(isHistory: true);
    }

    [RelayCommand]
    private void NavigateToAbout()
    {
        CurrentPage = _serviceProvider.GetRequiredService<AboutViewModel>();
        UpdateSelection(isAbout: true);
    }

    private void UpdateSelection(bool isHome = false, bool isHistory = false, bool isAbout = false)
    {
        IsHomeActive = isHome;
        IsHistoryActive = isHistory;
        IsAboutActive = isAbout;
    }
}
