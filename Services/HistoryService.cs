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
    private readonly string _filePath;

    public HistoryService()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CheckDuplicate");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "history.json");
    }

    public event EventHandler<HistorySession>? SessionSaved;

    public async Task SaveSessionAsync(HistorySession session)
    {
        var sessions = await LoadSessionsAsync();
        sessions.Insert(0, session); // Add to top
        await SaveAllAsync(sessions);
        
        SessionSaved?.Invoke(this, session);
    }

    public async Task<List<HistorySession>> LoadSessionsAsync()
    {
        if (!File.Exists(_filePath)) return new List<HistorySession>();

        try
        {
            using var stream = File.OpenRead(_filePath);
            var sessions = await JsonSerializer.DeserializeAsync(stream, Serialization.AppJsonContext.Default.ListHistorySession);
            return sessions ?? new List<HistorySession>();
        }
        catch
        {
            return new List<HistorySession>();
        }
    }

    public async Task DeleteSessionAsync(string id)
    {
        var sessions = await LoadSessionsAsync();
        var toRemove = sessions.FirstOrDefault(x => x.Id == id);
        if (toRemove != null)
        {
            sessions.Remove(toRemove);
            await SaveAllAsync(sessions);
        }
    }

    private async Task SaveAllAsync(List<HistorySession> sessions)
    {
        using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, sessions, Serialization.AppJsonContext.Default.ListHistorySession);
    }
}
