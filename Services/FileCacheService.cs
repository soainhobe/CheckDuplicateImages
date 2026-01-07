using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace CheckDuplicate.Services;

public class CacheEntry
{
    public string FilePath { get; set; } = string.Empty;
    public DateTime LastWriteTime { get; set; }
    public long FileSize { get; set; }
    public string HashData { get; set; } = string.Empty; // JSON or String representation of the hash
}

public interface IFileCacheService
{
    bool TryGet(string filePath, out CacheEntry? entry);
    void AddOrUpdate(string filePath, DateTime lastWriteTime, long fileSize, string hashData);
    Task LoadAsync();
    Task SaveAsync();
}

public class FileCacheService : IFileCacheService
{
    private readonly string _cacheFilePath;
    private ConcurrentDictionary<string, CacheEntry> _cache = new();

    public FileCacheService()
    {
        // Save to AppData or local folder
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "CheckDuplicateApp");
        Directory.CreateDirectory(folder);
        _cacheFilePath = Path.Combine(folder, "file_cache.json");
        
        // Load immediately or lazy? Better to have explicit Load.
    }

    public async Task LoadAsync()
    {
        if (!File.Exists(_cacheFilePath)) return;

        try
        {
            using var stream = File.OpenRead(_cacheFilePath);
            var list = await JsonSerializer.DeserializeAsync(stream, Serialization.AppJsonContext.Default.ListCacheEntry);
            if (list != null)
            {
                foreach (var item in list)
                {
                    _cache[item.FilePath] = item;
                }
            }
        }
        catch 
        {
            // Ignore corrupted cache
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var list = new List<CacheEntry>(_cache.Values);
            using var stream = File.Create(_cacheFilePath);
            await JsonSerializer.SerializeAsync(stream, list, Serialization.AppJsonContext.Default.ListCacheEntry);
        }
        catch
        {
            // Ignore save errors
        }
    }

    public bool TryGet(string filePath, out CacheEntry? entry)
    {
        return _cache.TryGetValue(filePath, out entry);
    }

    public void AddOrUpdate(string filePath, DateTime lastWriteTime, long fileSize, string hashData)
    {
        var entry = new CacheEntry
        {
            FilePath = filePath,
            LastWriteTime = lastWriteTime,
            FileSize = fileSize,
            HashData = hashData
        };
        _cache[filePath] = entry;
    }
}
