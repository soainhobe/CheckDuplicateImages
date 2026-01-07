using System;
using System.Collections.Generic;

namespace CheckDuplicate.Models;

public class HistorySession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Date { get; set; } = DateTime.Now;
    public int TotalDuplicates { get; set; }
    public long TotalSizeSaved { get; set; }
    public List<DuplicateFileItem> Items { get; set; } = new();
}
