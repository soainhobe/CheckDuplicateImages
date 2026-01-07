using System.Collections.Generic;
using System.Threading.Tasks;

namespace CheckDuplicate.Services;

public interface IFileService
{
    Task<string[]> ReadLinesAsync(string filePath);
    Task WriteLinesAsync(string filePath, IEnumerable<string> lines);
    void OpenFolder(string filePath);
    void DeleteToRecycleBin(string filePath);
}
