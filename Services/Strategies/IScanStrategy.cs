using System.Collections.Generic;
using System.Threading.Tasks;
using CheckDuplicate.Models;

namespace CheckDuplicate.Services.Strategies;

public interface IScanStrategy
{
    Task<IList<DuplicateFileItem>> ScanAsync(IEnumerable<string> paths, ScanConfiguration config, System.IProgress<double>? progress = null);
}
