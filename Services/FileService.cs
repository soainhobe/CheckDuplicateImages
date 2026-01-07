using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CheckDuplicate.Services;

public class FileService : IFileService
{
    public async Task<string[]> ReadLinesAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return System.Array.Empty<string>();
            
        return await File.ReadAllLinesAsync(filePath);
    }

    public async Task WriteLinesAsync(string filePath, IEnumerable<string> lines)
    {
        await File.WriteAllLinesAsync(filePath, lines);
    }

    public void OpenFolder(string filePath)
    {
        if (!File.Exists(filePath)) return;
        
        // Select file in explorer
        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
    }

    public void DeleteToRecycleBin(string filePath)
    {
        if (!File.Exists(filePath)) return;

        try
        {
            // Try VisualBasic way for Recycle Bin
            // This requires Microsoft.VisualBasic assembly reference. 
            // If not available, we might fallback to permanent delete or error?
            // User requested Recycle Bin specifically.
            // Let's assume it's available or we can use it.
            // Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(filePath, 
            //    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, 
            //    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            
            // Since I cannot guarantee the reference is added in csproj without checking,
            // I will use a safe approach or standard Delete if VB is missing.
            // But standard Delete is permanent.
            // Let's try to simple delete for now to ensure compilation, 
            // BUT adding a comment that this needs the VB ref.
            // Wait, I am an intelligent agent. I can check csproj?
            // I'll just look at csproj.
            
            // FOR NOW: Permanent Delete as fallback if I can't check/add ref quickly without risk.
            // BUT User asked for Recycle Bin.
            // I will try to use the VB namespace.
            
             Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(filePath, 
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, 
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        }
        catch
        {
            // Fallback or rethrow?
            // If VB is missing, this won't compile.
            // If runtime error, handle it.
            throw; 
        }
    }
}
