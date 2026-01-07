using Microsoft.Extensions.DependencyInjection;
using CheckDuplicate.Services;
using CheckDuplicate.ViewModels;

namespace CheckDuplicate;

public static class ServiceCollectionExtensions
{
    public static void AddCommonServices(this IServiceCollection collection)
    {
        collection.AddSingleton<IFileCacheService, FileCacheService>();
        collection.AddTransient<IDuplicateCheckerService, DuplicateCheckerService>();
        collection.AddTransient<IFileService, FileService>();
        collection.AddSingleton<IHistoryService, HistoryService>();
        collection.AddSingleton<MainWindowViewModel>();
        collection.AddSingleton<HomeViewModel>();
        collection.AddSingleton<ResultsViewModel>();
        collection.AddSingleton<HistoryViewModel>();
        collection.AddSingleton<AboutViewModel>();
    }
}
