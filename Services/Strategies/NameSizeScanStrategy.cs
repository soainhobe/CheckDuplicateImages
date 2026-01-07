using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CheckDuplicate.Models;

namespace CheckDuplicate.Services.Strategies;

public class NameSizeScanStrategy : IScanStrategy
{
    public Task<IList<DuplicateFileItem>> ScanAsync(IEnumerable<string> paths, ScanConfiguration config, System.IProgress<ScanProgress>? progress = null)
    {
        progress?.Report(new ScanProgress { ProcessedCount = 0, TotalCount = 0, CurrentFile = "Initializing..." });
        var result = new List<DuplicateFileItem>();
        
        // 1. Collect all files
        var allFiles = CollectFiles(paths, config);
        int totalFiles = allFiles.Count;
        
        progress?.Report(new ScanProgress { ProcessedCount = 0, TotalCount = totalFiles, CurrentFile = "Grouping files..." });

        // 2. Group by Name + Size -> Changed to Size Only as per request to find matches with different names
        var groups = allFiles
            .GroupBy(f => f.Length)
            .ToList();

        foreach (var g in groups)
        {
            bool isDuplicate = g.Count() > 1;
            string category = "Single file";
            string detailsGroup = string.Empty; // For singles
            
            if (isDuplicate)
            {
                category = "Duplicate File";
                var first = g.First();
                detailsGroup = $"{first.Name} ({FormatSize(first.Length)}) - {g.Count()} files";
            }

            foreach (var file in g)
            {
                result.Add(MapToItem(file, category, detailsGroup, isDuplicate));
            }
        }

        progress?.Report(new ScanProgress { ProcessedCount = totalFiles, TotalCount = totalFiles, CurrentFile = "Complete" });
        return Task.FromResult<IList<DuplicateFileItem>>(result);
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
