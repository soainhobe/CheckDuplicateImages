using CommunityToolkit.Mvvm.ComponentModel;

namespace CheckDuplicate.ViewModels;

public partial class AboutViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "About";
    
    [ObservableProperty]
    private string _appName = "Duplicate File Manager";
    
    [ObservableProperty]
    private string _appVersion = "1.0.0";
    
    [ObservableProperty]
    private string _description = "A powerful tool to scan, detect, and remove duplicate files to free up disk space. Supports advanced image comparison and smart content hashing.";
    
    [ObservableProperty]
    private string _companyName = "BTFSoft";
    
    [ObservableProperty]
    private string _website = "btfsoft.net";
    
    [ObservableProperty]
    private string _email = "Support@btfsof.net";
    
    [ObservableProperty]
    private string _copyright = "© 2026 BTFSoft. All rights reserved.";
}
