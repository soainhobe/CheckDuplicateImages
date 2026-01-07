using CommunityToolkit.Mvvm.ComponentModel;

namespace CheckDuplicate.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _welcomeMessage = "Welcome to Duplicate File Manager";
}
