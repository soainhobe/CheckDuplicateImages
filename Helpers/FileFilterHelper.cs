using System.Collections.Generic;
using System.IO;
using System.Linq;
using CheckDuplicate.Models;

namespace CheckDuplicate.Helpers;

public static class FileFilterHelper
{
    private static readonly HashSet<string> ImageExtensions = new() { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".ico" };
    private static readonly HashSet<string> VideoExtensions = new() { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm" };
    private static readonly HashSet<string> AudioExtensions = new() { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a" };
    private static readonly HashSet<string> DocumentExtensions = new() { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".rtf" };

    public static IEnumerable<FileInfo> ApplyFilters(IEnumerable<FileInfo> files, ScanConfiguration config)
    {
        var otherExtensions = config.OtherExtensions
            .Split(new[] { ',', ';', ' ' }, System.StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim().ToLower().StartsWith(".") ? e.Trim().ToLower() : "." + e.Trim().ToLower())
            .ToHashSet();

        return files.Where(f =>
        {
            var ext = f.Extension.ToLower();
            
            if (config.IncludeImages && ImageExtensions.Contains(ext)) return true;
            if (config.IncludeVideos && VideoExtensions.Contains(ext)) return true;
            if (config.IncludeMusic && AudioExtensions.Contains(ext)) return true;
            if (config.IncludeDocuments && DocumentExtensions.Contains(ext)) return true;
            if (config.IncludeOther && otherExtensions.Contains(ext)) return true;
            
            // If Other is checked but no extensions provided, does it match everything remaining? 
            // Usually "Other" implies custom extensions. 
            // If ALL are unchecked, nothing matches.
            
            return false;
        });
    }
}
