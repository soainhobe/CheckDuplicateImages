namespace CheckDuplicate.Models;

public struct ScanProgress
{
    public int ProcessedCount { get; set; }
    public int TotalCount { get; set; }
    public string CurrentFile { get; set; }
    
    public double Percentage => TotalCount > 0 ? (double)ProcessedCount / TotalCount * 100 : 0;
}
