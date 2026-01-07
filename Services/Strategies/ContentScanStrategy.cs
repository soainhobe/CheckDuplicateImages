using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CheckDuplicate.Helpers;
using CheckDuplicate.Models;

namespace CheckDuplicate.Services.Strategies;

public class ContentScanStrategy : IScanStrategy
{
    private readonly IFileCacheService? _cacheService;

    public ContentScanStrategy(IFileCacheService? cacheService = null)
    {
        _cacheService = cacheService;
    }

    public async Task<IList<DuplicateFileItem>> ScanAsync(IEnumerable<string> paths, ScanConfiguration config, System.IProgress<ScanProgress>? progress = null)
    {
        progress?.Report(new ScanProgress { ProcessedCount = 0, TotalCount = 0, CurrentFile = "Initializing..." });
        var result = new List<DuplicateFileItem>();

        // 1. Collect
        var allFiles = CollectFiles(paths, config);
        int totalFilesCount = allFiles.Count;
        progress?.Report(new ScanProgress { ProcessedCount = 0, TotalCount = totalFilesCount, CurrentFile = "Grouping by size..." });

        // 2. Group by Size (Optimization)
        var sizeGroups = allFiles.GroupBy(f => f.Length).ToList();

        var duplicates = new ConcurrentBag<FileInfo>();
        var singles = new ConcurrentBag<FileInfo>();

        int processedGlobal = 0;

        // 3. Process groups
        foreach (var group in sizeGroups)
        {
            if (group.Count() == 1)
            {
                singles.Add(group.First());
                int p = System.Threading.Interlocked.Increment(ref processedGlobal);
                // Report occasionally? Or every time? For smooth UI let's report e.g. every 10 or 100
                if (p % 10 == 0 || p == totalFilesCount)
                     progress?.Report(new ScanProgress { ProcessedCount = p, TotalCount = totalFilesCount, CurrentFile = group.First().Name });
                continue;
            }

            // Calculate Hash
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
                    // Failed to read, treat as single or error
                    singles.Add(file);
                }
                finally
                {
                    int p = System.Threading.Interlocked.Increment(ref processedGlobal);
                    if (p % 5 == 0 || p == totalFilesCount)
                         progress?.Report(new ScanProgress { ProcessedCount = p, TotalCount = totalFilesCount, CurrentFile = file.Name });
                }
            });

            foreach (var hashGroup in fileHashes)
            {
                if (hashGroup.Value.Count > 1)
                {
                    var first = hashGroup.Value.First();
                    // Level 2 Group Name
                    string detailsGroup = $"{first.Name} ({FormatSize(first.Length)}) - {hashGroup.Value.Count} files";
                    
                    foreach (var f in hashGroup.Value) 
                    {
                        result.Add(MapToItem(f, "Duplicate File", detailsGroup, true));
                    }
                }
                else
                {
                    foreach (var f in hashGroup.Value) singles.Add(f);
                }
            }
        }

        // 4. Map to Result
        // Duplicates already added with detailed groups
        // Only map Singles now
        foreach (var f in singles)
        {
            result.Add(MapToItem(f, "Single file", string.Empty, false));
        }

        progress?.Report(new ScanProgress { ProcessedCount = totalFilesCount, TotalCount = totalFilesCount, CurrentFile = "Complete" });
        return result;
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
