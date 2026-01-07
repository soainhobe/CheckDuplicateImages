using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CheckDuplicate.Helpers;
using CheckDuplicate.Models;

namespace CheckDuplicate.Services.Strategies;

public class SmartScanStrategy : IScanStrategy
{
    private readonly IFileCacheService? _cacheService;
    private long _lastReportTime;

    public SmartScanStrategy(IFileCacheService? cacheService = null)
    {
        _cacheService = cacheService;
    }

    public async Task<IList<DuplicateFileItem>> ScanAsync(IEnumerable<string> paths, ScanConfiguration config, System.IProgress<ScanProgress>? progress = null)
    {
        progress?.Report(new ScanProgress { ProcessedCount = 0, TotalCount = 0, CurrentFile = "Initializing..." });
        var result = new List<DuplicateFileItem>();

        // 1. Collect
        var allFiles = CollectFiles(paths, config);
        int totalFiles = allFiles.Count;
        
        progress?.Report(new ScanProgress { ProcessedCount = 0, TotalCount = totalFiles, CurrentFile = "Grouping by size..." });

        // 2. Group by Size (Quick Filter) - Ignore Name to find renamed duplicates
        var sizeNameGroups = allFiles.GroupBy(f => f.Length).ToList();

        // 3. Process suspects
        int processedCount = 0;

        foreach (var group in sizeNameGroups)
        {
            if (group.Count() == 1)
            {
                // Single file by size -> Not a duplicate. 
                // Don't track it to save memory.
                processProgress();
                continue;
            }

            // Suspects: Name and Size match. Now verify Content.
            var fileHashes = new ConcurrentDictionary<string, List<FileInfo>>();

            await Parallel.ForEachAsync(group, async (file, ct) =>
            {
                try
                {
                    string hash = string.Empty;
                    bool cached = false;

                    // 1. Check Cache
                    if (_cacheService != null && _cacheService.TryGet(file.FullName, out var entry))
                    {
                        if (entry != null && entry.FileSize == file.Length && entry.LastWriteTime == file.LastWriteTime)
                        {
                            hash = entry.HashData;
                            cached = true;
                        }
                    }

                    // 2. Compute if missing
                    if (!cached)
                    {
                        if (config.Strength == ComparisonStrength.Strict)
                        {
                            hash = await HashHelper.ComputeMd5Async(file.FullName);
                        }
                        else
                        {
                            hash = await HashHelper.ComputeCrc32Async(file.FullName, isPartial: true);
                        }

                        // 3. Update Cache
                        if (!string.IsNullOrEmpty(hash) && _cacheService != null)
                        {
                            _cacheService.AddOrUpdate(file.FullName, file.LastWriteTime, file.Length, hash);
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(hash))
                    {
                        fileHashes.AddOrUpdate(hash, 
                            new List<FileInfo> { file }, 
                            (k, v) => { lock(v) { v.Add(file); } return v; });
                    }
                }
                catch
                {
                    // Ignore errors
                }
                finally
                {
                    processProgress();
                }
            });

            foreach (var hashGroup in fileHashes)
            {
                if (hashGroup.Value.Count > 1)
                {
                    var first = hashGroup.Value.First();
                    string detailsGroup = $"{first.Name} ({FormatSize(first.Length)}) - {hashGroup.Value.Count} files";

                    foreach (var f in hashGroup.Value) 
                    {
                        lock(result)
                        {
                            result.Add(MapToItem(f, "Duplicate File", detailsGroup, true));
                        }
                    }
                }
            }
        }

        return result;

        // Local helper for progress to deduplicate code
        void processProgress()
        {
            int p = System.Threading.Interlocked.Increment(ref processedCount);
            
            // Throttling: only report every 100ms or if complete
            long now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long last = System.Threading.Interlocked.Read(ref _lastReportTime);
            
            if (now - last > 100 || p == totalFiles)
            {
                if (now - last > 100) 
                {
                    long original = System.Threading.Interlocked.Exchange(ref _lastReportTime, now);
                    if (now - original > 100 || p == totalFiles)
                    {
                        progress?.Report(new ScanProgress { ProcessedCount = p, TotalCount = totalFiles, CurrentFile = "Scanning..." });
                    }
                }
                else if (p == totalFiles)
                {
                     progress?.Report(new ScanProgress { ProcessedCount = p, TotalCount = totalFiles, CurrentFile = "Complete" });
                }
            }
        }
    }

    private List<FileInfo> CollectFiles(IEnumerable<string> paths, ScanConfiguration config)
    {
        var files = new List<FileInfo>();
        var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };
        
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    var dirInfo = new DirectoryInfo(path);
                    var collected = dirInfo.EnumerateFiles("*", options);
                    files.AddRange(Helpers.FileFilterHelper.ApplyFilters(collected, config));
                }
                catch { }
            }
        }
        return files;
    }

    private DuplicateFileItem MapToItem(FileInfo f, string category, string detailsGroup, bool selected)
    {
        return new DuplicateFileItem
        {
            FileName = f.Name,
            FullPath = f.FullName,
            FileLocation = f.DirectoryName ?? string.Empty,
            Size = FormatSize(f.Length),
            SizeBytes = f.Length,
            CreatedDate = f.CreationTime,
            Category = category,
            DetailsGroup = detailsGroup,
            IsSelected = selected
        };
    }

    private string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.0} {sizes[order]}";
    }
}
