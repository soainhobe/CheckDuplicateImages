using System.Collections.Generic;
using System.Threading.Tasks;
using CheckDuplicate.Models;

namespace CheckDuplicate.Services;

public interface IHistoryService
{
    Task SaveSessionAsync(Models.HistorySession session);
    Task<System.Collections.Generic.List<Models.HistorySession>> LoadSessionsAsync();
    Task<Models.HistorySession?> LoadSessionDetailsAsync(string id);
    Task DeleteSessionAsync(string id);
    
    event System.EventHandler<HistorySession>? SessionSaved;
}
