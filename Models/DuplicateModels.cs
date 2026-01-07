using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CheckDuplicate.Models;

public class DuplicateFileItem : ObservableObject
{
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string FileLocation { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedDate { get; set; }
    public bool IsSelected { get; set; }
    
    // Level 1 Group: "Duplicate File" or "Single file"
    public string Category { get; set; } = string.Empty;
    
    // Level 2 Group: Specific Set e.g. "Image.jpg (5MB)"
    public string DetailsGroup { get; set; } = string.Empty;

    public bool IsImage 
    {
        get 
        {
            if (string.IsNullOrEmpty(FileName)) return false;
            var ext = System.IO.Path.GetExtension(FileName).ToLower();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".gif" || ext == ".webp";
        }
    }

    private Task<Avalonia.Media.Imaging.Bitmap?>? _thumbnailTask;
    [System.Text.Json.Serialization.JsonIgnore]
    public Task<Avalonia.Media.Imaging.Bitmap?> ThumbnailTask => _thumbnailTask ??= LoadThumbnailAsync();

    private async Task<Avalonia.Media.Imaging.Bitmap?> LoadThumbnailAsync()
    {
        if (!IsImage || string.IsNullOrEmpty(FullPath)) return null;

        return await Task.Run(() =>
        {
            try
            {
                if (System.IO.File.Exists(FullPath))
                {
                    using var stream = System.IO.File.OpenRead(FullPath);
                    // Decode to specific width (e.g. 100px) to save massive memory and time
                    return Avalonia.Media.Imaging.Bitmap.DecodeToWidth(stream, 100);
                }
            }
            catch
            {
                // Ignore errors
            }
            return null;
        });
    }
}

public class DuplicateFileGroup : ObservableCollection<DuplicateFileItem>
{
    public string GroupName { get; set; } = string.Empty;
    public string CountInfo { get; set; } = string.Empty; // e.g. "(3)"
    
    public DuplicateFileGroup(string groupName, System.Collections.Generic.IEnumerable<DuplicateFileItem> items) : base(items)
    {
        GroupName = groupName;
        CountInfo = $"({Items.Count})";
    }
}
