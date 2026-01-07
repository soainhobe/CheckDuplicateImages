using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CheckDuplicate.Models;

namespace CheckDuplicate.Services;

public class HistoryService : IHistoryService
{
    private readonly string _folderPath;
    private readonly string _legacyFilePath;

    public HistoryService()
    {
        _folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CheckDuplicate", "Sessions");
        Directory.CreateDirectory(_folderPath);
        
        // Legacy path for migration
        _legacyFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CheckDuplicate", "history.json");
    }

    public event EventHandler<HistorySession>? SessionSaved;

    public async Task SaveSessionAsync(HistorySession session)
    {
        // Split Data: 
        // 1. Meta (Session info without items)
        // 2. Data (Items list)
        
        // Clone for meta (shallow copy is enough if we clear list)
        var meta = new HistorySession 
        { 
            Id = session.Id, 
            Date = session.Date, 
            TotalDuplicates = session.TotalDuplicates, 
            TotalSizeSaved = session.TotalSizeSaved,
            Items = new List<DuplicateFileItem>() // Empty for meta
        };

        var items = session.Items;

        string metaPath = Path.Combine(_folderPath, $"meta_{session.Id}.json");
        string dataPath = Path.Combine(_folderPath, $"data_{session.Id}.json");

        using (var ms = File.Create(metaPath))
        {
            await JsonSerializer.SerializeAsync(ms, meta, Serialization.AppJsonContext.Default.HistorySession);
        }

        using (var ds = File.Create(dataPath))
        {
            await JsonSerializer.SerializeAsync(ds, items, Serialization.AppJsonContext.Default.ListDuplicateFileItem);
        }
        
        SessionSaved?.Invoke(this, session);
    }

    public async Task<List<HistorySession>> LoadSessionsAsync()
    {
        await MigrateLegacyAsync();

        var sessions = new List<HistorySession>();
        var metaFiles = Directory.GetFiles(_folderPath, "meta_*.json");

        foreach (var file in metaFiles)
        {
            try
            {
                using var stream = File.OpenRead(file);
                var session = await JsonSerializer.DeserializeAsync(stream, Serialization.AppJsonContext.Default.HistorySession);
                if (session != null)
                {
                    sessions.Add(session);
                }
            }
            catch { /* Ignore corrupt files */ }
        }

        return sessions.OrderByDescending(s => s.Date).ToList();
    }

    public async Task<HistorySession?> LoadSessionDetailsAsync(string id)
    {
        string dataPath = Path.Combine(_folderPath, $"data_{id}.json");
        if (!File.Exists(dataPath)) return null; // Or return empty session?

        try
        {
            using var stream = File.OpenRead(dataPath);
            var items = await JsonSerializer.DeserializeAsync(stream, Serialization.AppJsonContext.Default.ListDuplicateFileItem);
            
            // We need the meta too effectively, usually this is called on an existing object.
            // But interface returns HistorySession.
            // We can return just a partial session with Items, or caller merges.
            // Let's load Meta to be safe and return full object.
            
            string metaPath = Path.Combine(_folderPath, $"meta_{id}.json");
            HistorySession? session = null;
            if (File.Exists(metaPath))
            {
                using var ms = File.OpenRead(metaPath);
                session = await JsonSerializer.DeserializeAsync(ms, Serialization.AppJsonContext.Default.HistorySession);
            }

            if (session != null)
            {
                session.Items = items ?? new List<DuplicateFileItem>();
                return session;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task DeleteSessionAsync(string id)
    {
        string metaPath = Path.Combine(_folderPath, $"meta_{id}.json");
        string dataPath = Path.Combine(_folderPath, $"data_{id}.json");

        await Task.Run(() => 
        {
            if (File.Exists(metaPath)) File.Delete(metaPath);
            if (File.Exists(dataPath)) File.Delete(dataPath);
        });
    }

    private async Task MigrateLegacyAsync()
    {
        if (!File.Exists(_legacyFilePath)) return;

        try
        {
            List<HistorySession>? oldSessions;
            using (var stream = File.OpenRead(_legacyFilePath))
            {
                oldSessions = await JsonSerializer.DeserializeAsync(stream, Serialization.AppJsonContext.Default.ListHistorySession);
            }

            if (oldSessions != null)
            {
                foreach (var s in oldSessions)
                {
                    await SaveSessionAsync(s);
                }
            }

            // Rename legacy to .bak
            File.Move(_legacyFilePath, _legacyFilePath + ".bak");
        }
        catch
        {
            // If migration fails, ignore (maybe corrupt). 
            // Don't retry endlessly.
            try { File.Move(_legacyFilePath, _legacyFilePath + ".corrupt"); } catch { }
        }
    }
}
