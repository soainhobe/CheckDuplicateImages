using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CheckDuplicate.Models;
using CheckDuplicate.Services.Strategies;

namespace CheckDuplicate.Services;

public class DuplicateCheckerService : IDuplicateCheckerService
{
    private readonly IFileCacheService _cacheService;

    public DuplicateCheckerService(IFileCacheService cacheService)
    {
        _cacheService = cacheService;
        // Ensure cache is loaded. In a real app, this might be done at startup.
        // For now, fire and forget or wait? Better to wait implicitly or explicit init.
        // Let's assume loading is managed by the caller or we trigger it here lazily.
        // Since constructor can't be async, we'll kick it off.
        _ = _cacheService.LoadAsync();
    }

    public async Task<IList<DuplicateFileItem>> ScanAsync(ScanConfiguration config, IProgress<ScanProgress>? progress = null)
    {
        IScanStrategy strategy = config.Method switch
        {
            ComparisonMethod.NameAndSize => new NameSizeScanStrategy(),
            ComparisonMethod.Content => new ContentScanStrategy(_cacheService),
            ComparisonMethod.AdvancedImageScan => new AdvancedImageScanStrategy(_cacheService),
            ComparisonMethod.Auto => new SmartScanStrategy(_cacheService),
            _ => new SmartScanStrategy(_cacheService)
        };

        var results = await Task.Run(() => strategy.ScanAsync(config.SearchPaths, config, progress));
        
        // Auto-save cache after scan to persist new results
        await _cacheService.SaveAsync();
        
        return results;
    }
}
