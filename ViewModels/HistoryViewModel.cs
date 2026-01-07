using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CheckDuplicate.Models;
using CheckDuplicate.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CheckDuplicate.ViewModels;

public partial class HistoryViewModel : ViewModelBase
{
    private readonly IHistoryService _historyService;

    [ObservableProperty]
    private string _title = "Scan History";

    public ObservableCollection<HistorySession> Sessions { get; } = new();

    [ObservableProperty]
    private HistorySession? _selectedSession;

    // Design-time
    public HistoryViewModel() 
    {
        _historyService = null!;
    }

    public HistoryViewModel(IHistoryService historyService)
    {
        _historyService = historyService;
        _historyService.SessionSaved += OnSessionSaved;
        Initialize();
    }

    private void OnSessionSaved(object? sender, HistorySession session)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Sessions.Insert(0, session);
        });
    }

    private async void Initialize()
    {
        try
        {
            await LoadHistory();
        }
        catch
        {
            // Logging would go here. Prevent crash.
        }
    }

    [RelayCommand]
    private async Task LoadHistory()
    {
        Sessions.Clear();
        var data = await _historyService.LoadSessionsAsync();
        foreach (var session in data)
        {
            Sessions.Add(session);
        }
    }

    [RelayCommand]
    private async Task DeleteSession(HistorySession? session)
    {
        if (session == null) return;
        
        await _historyService.DeleteSessionAsync(session.Id);
        Sessions.Remove(session);
        
        if (SelectedSession == session) SelectedSession = null;
    }

    partial void OnSelectedSessionChanged(HistorySession? value)
    {
        // Could trigger navigation or detail view update
    }
}
