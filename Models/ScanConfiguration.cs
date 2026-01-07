using System.Collections.Generic;

namespace CheckDuplicate.Models;

public enum ComparisonMethod
{
    Content,
    NameAndSize,
    Auto,
    AdvancedImageScan // dHash (Similarity)
}

public enum ComparisonStrength
{
    Strict, // MD5 Full
    Loose   // CRC32 Partial
}

public class ScanConfiguration
{
    public IEnumerable<string> SearchPaths { get; set; } = new List<string>();
    public ComparisonMethod Method { get; set; }
    public ComparisonStrength Strength { get; set; }
    
    // Filters
    public bool IncludeImages { get; set; }
    public bool IncludeVideos { get; set; }
    public bool IncludeDocuments { get; set; }
    public bool IncludeMusic { get; set; }
    public bool IncludeOther { get; set; }
    public string OtherExtensions { get; set; } = string.Empty;
}
