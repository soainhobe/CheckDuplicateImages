using System.Collections.Generic;
using System.Threading.Tasks;
using CheckDuplicate.Models;

namespace CheckDuplicate.Services;

public interface IHistoryService
{
    Task SaveSessionAsync(HistorySession session);
    Task<List<HistorySession>> LoadSessionsAsync();
    Task DeleteSessionAsync(string id);
    
    event System.EventHandler<HistorySession>? SessionSaved;
}

