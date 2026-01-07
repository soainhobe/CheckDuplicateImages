using System;
using System.IO;
using System.Security.Cryptography;
using System.IO.Hashing;
using System.Threading.Tasks;

namespace CheckDuplicate.Helpers;

public static class HashHelper
{
    private const int BUFFER_SIZE = 8192;
    private const int PARTIAL_CHUNK_SIZE = 4096; // 4KB

    public static async Task<string> ComputeMd5Async(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var md5 = MD5.Create();
        var hashBytes = await md5.ComputeHashAsync(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    public static async Task<string> ComputeCrc32Async(string filePath, bool isPartial)
    {
        if (!isPartial)
        {
            // Full CRC32
            using var stream = File.OpenRead(filePath);
            var crc32 = new Crc32();
            await crc32.AppendAsync(stream);
            return BitConverter.ToString(crc32.GetCurrentHash()).Replace("-", "").ToLowerInvariant();
        }
        else
        {
            // Partial: Head, Middle, Tail
            using var stream = File.OpenRead(filePath);
            var length = stream.Length;
            var crc32 = new Crc32();

            // Head
            await AppendChunkAsync(stream, crc32, 0);

            // Middle
            if (length > PARTIAL_CHUNK_SIZE)
            {
                long midPos = (length / 2) - (PARTIAL_CHUNK_SIZE / 2);
                if (midPos < 0) midPos = 0;
                await AppendChunkAsync(stream, crc32, midPos);
            }

            // Tail
            if (length > PARTIAL_CHUNK_SIZE * 2)
            {
                long tailPos = length - PARTIAL_CHUNK_SIZE;
                await AppendChunkAsync(stream, crc32, tailPos);
            }

            return BitConverter.ToString(crc32.GetCurrentHash()).Replace("-", "").ToLowerInvariant();
        }
    }

    private static async Task AppendChunkAsync(FileStream stream, Crc32 crc32, long position)
    {
        if (position >= stream.Length) return;
        
        stream.Seek(position, SeekOrigin.Begin);
        var buffer = new byte[PARTIAL_CHUNK_SIZE];
        var bytesRead = await stream.ReadAsync(buffer, 0, PARTIAL_CHUNK_SIZE);
        if (bytesRead > 0)
        {
            // Only append the bytes read
            crc32.Append(buffer.AsSpan(0, bytesRead));
        }
    }
}
