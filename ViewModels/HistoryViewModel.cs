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

    async partial void OnSelectedSessionChanged(HistorySession? value)
    {
        if (value != null && (value.Items == null || value.Items.Count == 0))
        {
            // Lazy load items
            // Show loading?
            var fullSession = await _historyService.LoadSessionDetailsAsync(value.Id);
            if (fullSession != null)
            {
                value.Items = fullSession.Items;
                // Force UI update if needed. The collection inside 'value' changed.
                // PropertyChanged for 'SelectedSession' might have already fired.
                // If Items is bound, it needs notification.
                // Since 'Items' is a property of HistorySession which might not observe changes,
                // we might need to Refresh binding.
                
                // Hack: Re-set the property to trigger binding refresh
                OnPropertyChanged(nameof(SelectedSession));
            }
        }
    }
}
