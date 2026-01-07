using System.Collections.Generic;
using System.Threading.Tasks;
using CheckDuplicate.Models;

namespace CheckDuplicate.Services;

public interface IDuplicateCheckerService
{
    Task<IList<DuplicateFileItem>> ScanAsync(ScanConfiguration config, System.IProgress<ScanProgress>? progress = null);
}
