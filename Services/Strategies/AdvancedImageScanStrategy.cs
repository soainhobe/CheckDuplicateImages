using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CheckDuplicate.Helpers;
using CheckDuplicate.Models;

namespace CheckDuplicate.Services.Strategies;

public class AdvancedImageScanStrategy : IScanStrategy
{
    private const int SimilarityThreshold = 5; // Configurable?
    private readonly IFileCacheService? _cacheService;

    public AdvancedImageScanStrategy(IFileCacheService? cacheService = null)
    {
        _cacheService = cacheService;
    }

    public async Task<IList<DuplicateFileItem>> ScanAsync(IEnumerable<string> paths, ScanConfiguration config, System.IProgress<double>? progress = null)
    {
        var result = new List<DuplicateFileItem>();
        
        // 1. Collect Image Files Only
        // We force include images for this strategy regardless of config logic? 
        // Or respect config? Config might have "IncludeImages = false".
        // But user selected "Advanced Image Scan".
        // We should likely collect ALL images.
        var images = CollectImages(paths);
        
        if (!images.Any()) return result;

        // 2. Compute Hashes
        var fileHashes = new ConcurrentBag<(FileInfo File, DHashHelper.ImageHashes Hash)>();
        int totalImages = images.Count;
        int processed = 0;
        
        await Parallel.ForEachAsync(images, async (file, ct) =>
        {
            try
            {
                // Increment and report
                int p = System.Threading.Interlocked.Increment(ref processed);
                if (totalImages > 0) progress?.Report((double)p / totalImages * 100);
            DHashHelper.ImageHashes hash = default;
            bool cached = false;

            // 1. Check Cache
            if (_cacheService != null && _cacheService.TryGet(file.FullName, out var entry))
            {
                if (entry != null && entry.FileSize == file.Length && entry.LastWriteTime == file.LastWriteTime)
                {
                    try
                    {
                        hash = System.Text.Json.JsonSerializer.Deserialize(entry.HashData, Serialization.AppJsonContext.Default.ImageHashes);
                        cached = true;
                    }
                    catch { }
                }
            }

            // 2. Compute if missing
            if (!cached)
            {
                hash = await DHashHelper.ComputeHashesAsync(file.FullName);
                
                // 3. Update Cache
                if (hash.NormalHash != 0 && _cacheService != null)
                {
                    string json = System.Text.Json.JsonSerializer.Serialize(hash, Serialization.AppJsonContext.Default.ImageHashes);
                    _cacheService.AddOrUpdate(file.FullName, file.LastWriteTime, file.Length, json);
                }
            }

            if (hash.NormalHash != 0) 
            {
                fileHashes.Add((file, hash));
            }
            } catch { }
        });

        // 3. Grouping using VP-Tree or Linear method
        // For simplicity and correctness in this robust pass, we use a greedy clustering:
        // - Sort by Hash Magnitude (or random) to have stable order?
        // - Iterate, find match in existing groups.
        
        var files = fileHashes.ToList();
        var groups = new List<List<(FileInfo File, DHashHelper.ImageHashes Hash)>>();

        // Simple Greedy Clustering with Pivot
        // Optimization: Parallelize search against Pivot? 
        // Since groups list is growing, parallel is hard.
        // We can use a VP-Tree to speed up "Find Nearest".
        
        // Let's implement a simple Linear Pivot Scan.
        // For 1000 images, 1000 comparisons is nothing.
        // For 10,000 images, 100,000,000 comparisons is 100ms with XOR.
        // Simple linear scan is usually fine for < 50,000 images.
        
        foreach (var item in files)
        {
            bool matchFound = false;
            
            // Check against existing groups (Representative is first item)
            // Reverse loop might be slightly faster if recent groups match? No.
            // Parallel loop to find faster?
            
            // Note: We use the Pivot's NormalHash.
            // We compare Item.Normal vs Pivot.Normal
            // AND Item.Flipped vs Pivot.Normal (to catch if Item is flipped version of Pivot)
            
            // Optimization: If groups.Count is large, this loop is slow.
            // Implementation of VP-Tree here would be: Add Pivot to VP-Tree. Query VP-Tree.
            
            foreach (var g in groups)
            {
                var pivot = g[0];
                
                // Check Normal
                if (DHashHelper.HammingDistance(item.Hash.NormalHash, pivot.Hash.NormalHash) <= SimilarityThreshold)
                {
                    g.Add(item);
                    matchFound = true;
                    break;
                }
                
                // Check Flipped (Item is flipped relative to Pivot)
                if (DHashHelper.HammingDistance(item.Hash.FlippedHash, pivot.Hash.NormalHash) <= SimilarityThreshold)
                {
                    g.Add(item);
                    matchFound = true;
                    break;
                }
                
                // Check Center Hash (Crop detection - Type 1: Both are center crops or identical)
                if (DHashHelper.HammingDistance(item.Hash.CenterHash, pivot.Hash.CenterHash) <= SimilarityThreshold)
                {
                    g.Add(item);
                    matchFound = true;
                    break;
                }

                // Check Cross-Center
                if (DHashHelper.HammingDistance(item.Hash.NormalHash, pivot.Hash.CenterHash) <= SimilarityThreshold ||
                    DHashHelper.HammingDistance(pivot.Hash.NormalHash, item.Hash.CenterHash) <= SimilarityThreshold)
                {
                    g.Add(item);
                    matchFound = true;
                    break;
                }

                // Check SubRegions
                // If Item is a SubRegion of Pivot
                bool subRegionMatch = false;
                if (pivot.Hash.SubRegions != null)
                {
                    foreach (var qHash in pivot.Hash.SubRegions)
                    {
                        if (DHashHelper.HammingDistance(item.Hash.NormalHash, qHash) <= SimilarityThreshold)
                        {
                            subRegionMatch = true;
                            break;
                        }
                    }
                }
                
                // If Pivot is a SubRegion of Item
                if (!subRegionMatch && item.Hash.SubRegions != null)
                {
                    foreach (var qHash in item.Hash.SubRegions)
                    {
                        if (DHashHelper.HammingDistance(pivot.Hash.NormalHash, qHash) <= SimilarityThreshold)
                        {
                            subRegionMatch = true;
                            break;
                        }
                    }
                }

                if (subRegionMatch)
                {
                    g.Add(item);
                    matchFound = true;
                    break;
                }
            }
            
            if (!matchFound)
            {
                groups.Add(new List<(FileInfo, DHashHelper.ImageHashes)> { item });
            }
        }

        // 4. Map to Result
        foreach (var g in groups)
        {
            // Only add if we have duplicates (count > 1)
            if (g.Count > 1)
            {
                var first = g[0].File;
                // Category = Duplicate File
                // DetailsGroup = "Similar Images: Name (Size) - Count"
                string detailsGroup = $"Similar Images: {first.Name} ({FormatSize(first.Length)}) - {g.Count} files";

                foreach (var item in g)
                {
                    result.Add(MapToItem(item.File, "Duplicate File", detailsGroup, true));
                }
            }
            else
            {
                // Result for Single File
                result.Add(MapToItem(g[0].File, "Single file", string.Empty, false));
            }
        }

        return result;
    }

    private List<FileInfo> CollectImages(IEnumerable<string> paths)
    {
        var files = new List<FileInfo>();
        var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };
        
        // Custom simple config just for images
        // We emulate a config that only allows images
        var imgConfig = new ScanConfiguration 
        { 
            IncludeImages = true, 
            IncludeVideos = false, 
            IncludeMusic = false, 
            IncludeDocuments = false, 
            IncludeOther = false 
        };

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    var dirInfo = new DirectoryInfo(path);
                    var collected = dirInfo.EnumerateFiles("*", options);
                    files.AddRange(Helpers.FileFilterHelper.ApplyFilters(collected, imgConfig));
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
