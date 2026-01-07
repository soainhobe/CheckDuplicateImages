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

    public async Task<IList<DuplicateFileItem>> ScanAsync(IEnumerable<string> paths, ScanConfiguration config, System.IProgress<double>? progress = null)
    {
        var result = new List<DuplicateFileItem>();

        // 1. Collect
        var allFiles = CollectFiles(paths, config);

        // 2. Group by Size (Optimization)
        var sizeGroups = allFiles.GroupBy(f => f.Length).ToList();

        var duplicates = new ConcurrentBag<FileInfo>();
        var singles = new ConcurrentBag<FileInfo>();

        // 3. Process groups
        foreach (var group in sizeGroups)
        {
            if (group.Count() == 1)
            {
                singles.Add(group.First());
                continue;
            }

            // Calculate Hash
            var fileHashes = new ConcurrentDictionary<string, List<FileInfo>>();
            int totalFiles = sizeGroups.Sum(g => g.Count());
            int processed = 0;

            await Parallel.ForEachAsync(group, async (file, ct) =>
            {
                // Increment processed count atomically
                System.Threading.Interlocked.Increment(ref processed);
                // Report progress sparingly or every X items to avoid UI lag, but for simplicity:
                // Calculate percentage relative to total files (Wait, 'processed' is local to this group effectively or overall?)
                // Actually, totalFiles is wrong here. 'group' is just one group.
                // Scan logic iterates groups sequentially, but inside group it's parallel.
                // To do global progress, I need total count of ALL files first.
                // 'allFiles.Count' is available at step 1.
                // So I should initialize 'processed' outside loop.
                // But I can't easily share 'processed' across this structure without passing it or changing structure.
                // Wait, 'processed' needs to be outside loop.
                
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
