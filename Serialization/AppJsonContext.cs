using System.Collections.Generic;
using System.Text.Json.Serialization;
using CheckDuplicate.Models;
using CheckDuplicate.Services;

namespace CheckDuplicate.Serialization;

[JsonSerializable(typeof(List<HistorySession>))]
[JsonSerializable(typeof(List<CacheEntry>))]
[JsonSerializable(typeof(List<DuplicateFileItem>))]
[JsonSerializable(typeof(HistorySession))]
[JsonSerializable(typeof(CacheEntry))]
[JsonSerializable(typeof(DuplicateFileItem))]
[JsonSerializable(typeof(CheckDuplicate.Helpers.DHashHelper.ImageHashes))]
public partial class AppJsonContext : JsonSerializerContext
{
}
