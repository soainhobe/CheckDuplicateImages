using System;
using System.IO;
using System.Threading.Tasks;
using SkiaSharp;

namespace CheckDuplicate.Helpers;

public static class DHashHelper
{
    // dHash uses 9x8 size for 64-bit hash
    // We compare pixel[x] with pixel[x+1]
    private const int Width = 9;
    private const int Height = 8;
    
    public struct ImageHashes
    {
        public ulong NormalHash { get; set; }
        public ulong FlippedHash { get; set; }
        public ulong CenterHash { get; set; }
        public ulong[] SubRegions { get; set; } // [0]Left, [1]Right, [2]Top, [3]Bottom, [4]Center50
        public uint AverageColor { get; set; } // 0xAARRGGBB
    }

    public static async Task<ImageHashes> ComputeHashesAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                using var original = SKBitmap.Decode(stream);
                if (original == null) return new ImageHashes();

                var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
                
                // 0. Optimization: Downscale huge images to a reasonable working size (e.g. 256px)
                // This drastically speeds up "TrimTransparency" (scanning pixels) and reduces subsequent memory ops.
                var scaledBitmap = ResizeToWorkingSize(original, 256);
                var bitmapForTrim = scaledBitmap ?? original; // Use scaled if available, else original
                
                try
                {
                    // 1. Auto-Trim Transparency
                    using var trimmed = TrimTransparency(bitmapForTrim);
                    
                    // Use trimmed source if valid, else the working bitmap
                    var sourceToResize = trimmed ?? bitmapForTrim;

                    // FIX: Composite on WHITE background to handle transparency correctly
                    // Otherwise transparent pixels might become black in Grayscale conversion, creating false positives.
                    using var opaqueBitmap = RemoveTransparency(sourceToResize);
                    var finalSource = opaqueBitmap; 

                    // 2. Compute Global Hashes from the optimized source
                    var info = new SKImageInfo(Width, Height, SKColorType.Gray8);
                    using var resized = finalSource.Resize(info, sampling);
                    
                    ulong normalHash = 0;
                    ulong flippedHash = 0;
                    ulong centerHash = 0;
                    // We store multiple regions:
                    // 0-3: 2x2 Grid (TL, TR, BL, BR) - Quarters
                    // 4-12: 3x3 Grid (9 sectors) - Thirds
                    // 13: Center 50% (Tight)
                    ulong[] subRegions = new ulong[14]; 
    
                    if (resized != null)
                    {
                       CalculateGlobalHashes(resized, out normalHash, out flippedHash);
                    }
    
                    // 2. Compute Center Hash (Loose 75%)
                    // Use finalSource (opaque) for all subsequent checks
                    int w = finalSource.Width;
                    int h = finalSource.Height;
                    
                    centerHash = ComputeCropHash(finalSource, info, sampling, 
                        (int)(w * 0.75), (int)(h * 0.75), (w - (int)(w * 0.75)) / 2, (h - (int)(h * 0.75)) / 2);

                    // 3. Compute High-Density SubRegions
                    if (w > 1 && h > 1)
                    {
                        // A. 2x2 Grid (Quarters) -> Indices 0-3
                        int halfW = w / 2;
                        int halfH = h / 2;
                        subRegions[0] = ComputeCropHash(finalSource, info, sampling, halfW, halfH, 0, 0); // TL
                        subRegions[1] = ComputeCropHash(finalSource, info, sampling, w - halfW, halfH, halfW, 0); // TR
                        subRegions[2] = ComputeCropHash(finalSource, info, sampling, halfW, h - halfH, 0, halfH); // BL
                        subRegions[3] = ComputeCropHash(finalSource, info, sampling, w - halfW, h - halfH, halfW, halfH); // BR
                        
                        // B. 3x3 Grid (Thirds) -> Indices 4-12
                        int thirdW = w / 3;
                        int thirdH = h / 3;
                        // Avoid tiny slivers if width is small, but for >25px it's fine.
                        if (thirdW > 0 && thirdH > 0)
                        {
                            for (int r = 0; r < 3; r++)
                            {
                                for (int c = 0; c < 3; c++)
                                {
                                    int cw = (c == 2) ? (w - 2 * thirdW) : thirdW; // Handle reminder on last col
                                    int ch = (r == 2) ? (h - 2 * thirdH) : thirdH; // Handle reminder on last row
                                    subRegions[4 + r * 3 + c] = ComputeCropHash(finalSource, info, sampling, cw, ch, c * thirdW, r * thirdH);
                                }
                            }
                        }

                        // C. Center 50% (Tight Subject) -> Index 13
                        int tightW = (int)(w * 0.5);
                        int tightH = (int)(h * 0.5);
                        subRegions[13] = ComputeCropHash(finalSource, info, sampling, tightW, tightH, (w - tightW)/2, (h - tightH)/2);
                    }
    
                    // 2.5 Compute Average Color
                    uint avgColor = CalculateAverageColor(finalSource);

                    return new ImageHashes 
                    { 
                        NormalHash = normalHash, 
                        FlippedHash = flippedHash,
                        CenterHash = centerHash,
                        SubRegions = subRegions,
                        AverageColor = avgColor
                    };
                }
                finally
                {
                    scaledBitmap?.Dispose();
                    // Dispose opaqueBitmap if it was created
                    // It's implicitly disposed by the 'using' statement if it was created.
                    // If 'trimmed' was null, 'sourceToResize' would be 'bitmapForTrim', and 'opaqueBitmap' would be created from it.
                    // If 'trimmed' was not null, 'sourceToResize' would be 'trimmed', and 'opaqueBitmap' would be created from it.
                    // In both cases, 'opaqueBitmap' is a new SKBitmap and needs disposal.
                    // The 'using' statement handles it.
                }
                

            }
            catch
            {
                return new ImageHashes();
            }
        });
    }

    // Helper: Composite bitmap over white background to remove transparency
    private static SKBitmap RemoveTransparency(SKBitmap source)
    {
        // New bitmap with same dimensions, opaque
        var opaque = new SKBitmap(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        
        using (var canvas = new SKCanvas(opaque))
        {
            // Fill white
            canvas.Clear(SKColors.White);
            // Draw source over it
            canvas.DrawBitmap(source, 0, 0);
        }
        
        return opaque;
    }

    // Helper: Finds the bounding box of non-transparent pixels and crops it
    private static SKBitmap? TrimTransparency(SKBitmap source)
    {
        // If not containing alpha, return null (use original) to save time
        if (source.ColorType != SKColorType.Bgra8888 && source.ColorType != SKColorType.Rgba8888) 
            return null; // Optimistic check, though generic bitmaps might still have alpha in other formats.
            
        int width = source.Width;
        int height = source.Height;
        
        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;

        IntPtr pixelsAddr = source.GetPixels();
        
        unsafe
        {
            // Assuming 32-bit (4 bytes) per pixel
            // This is valid for standard Skia bitmaps loaded from files (usually 8888)
            byte* ptr = (byte*)pixelsAddr;
            int rowBytes = source.RowBytes; // RowBytes is total bytes per row

            for (int y = 0; y < height; y++)
            {
                byte* row = ptr + (y * rowBytes);
                for (int x = 0; x < width; x++)
                {
                    // Check Alpha. 
                    // SKColorType.Bgra8888 -> B G R A
                    // SKColorType.Rgba8888 -> R G B A
                    // In both 8888 formats, Alpha is usually the 4th byte (offset 3) 
                    // OR it depends on byte order (Little Endian).
                    // Actually, let's just use GetPixel for safety or check documentation?
                    // GetPixel is 100x slower.
                    // For typical ARGB/BGRA, Alpha is at offset 3.
                    
                    // Optimization: Check if *pixel* != 0 (implies some data).
                    // If A=0, pixel is invisible.
                    // Let's check the Alpha byte specifically.
                    // On Little Endian: 0xAARRGGBB. Alpha is MSB.
                    // So we can read int and check (val >> 24) != 0.
                    
                    uint* pixelPtr = (uint*)(row + x * 4);
                    uint pixel = *pixelPtr;
                    
                    // Alpha is in the high byte (0xAARRGGBB) for Little Endian uint read
                    // Optimization check: (pixel & 0xFF000000) is the alpha component shifted
                    uint alpha = (pixel & 0xFF000000) >> 24;
                    
                    if (alpha > 15) // Ignore faint shadows/noise (Threshold 15/255)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
        }

        // Check if we found anything
        if (maxX == -1) return null; // Fully transparent or empty

        // If bounds match original, return null
        if (minX == 0 && minY == 0 && maxX == width - 1 && maxY == height - 1) return null;

        // Crop
        var rect = new SKRectI(minX, minY, maxX + 1, maxY + 1);
        var trimmed = new SKBitmap(rect.Width, rect.Height);
        source.ExtractSubset(trimmed, rect);
        
        return trimmed;
    }

    private static ulong ComputeCropHash(SKBitmap source, SKImageInfo info, SKSamplingOptions sampling, int w, int h, int x, int y)
    {
        if (w <= 0 || h <= 0) return 0;
        var rect = new SKRectI(x, y, x + w, y + h);
        using var crop = new SKBitmap();
        if (source.ExtractSubset(crop, rect))
        {
             using var resized = crop.Resize(info, sampling);
             if (resized != null) return ComputeSingleHash(resized);
        }
        return 0;
    }

    private static SKBitmap? ResizeToWorkingSize(SKBitmap source, int maxDim)
    {
        if (source.Width <= maxDim && source.Height <= maxDim) return null; // No need to resize

        float scale = Math.Min((float)maxDim / source.Width, (float)maxDim / source.Height);
        int newW = (int)(source.Width * scale);
        int newH = (int)(source.Height * scale);

        var info = new SKImageInfo(newW, newH, source.ColorType); // Keep color type (likely 8888 for alpha)
        // Use Medium quality for this intermediate step to preserve alpha edges reasonably well without being too slow
        return source.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Nearest));
    }

    // Helper to calculate both Normal and Flipped in one pass from a resized 9x8 bitmap
    private static void CalculateGlobalHashes(SKBitmap resized, out ulong normal, out ulong flipped)
    {
        normal = 0;
        flipped = 0;
        IntPtr pixelsAddr = resized.GetPixels();
        
        unsafe 
        {
            byte* ptr = (byte*)pixelsAddr;
            int rowBytes = resized.RowBytes;

            for (int y = 0; y < Height; y++)
            {
                byte* row = ptr + (y * rowBytes);
                
                for (int x = 0; x < Width - 1; x++) 
                {
                    // Normal
                    if (row[x] < row[x + 1])
                        normal |= (1UL << (y * 8 + x));
                    
                    // Flipped: compare P[8-x] with P[7-x]
                    if (row[Width - 1 - x] < row[Width - 2 - x])
                        flipped |= (1UL << (y * 8 + x));
                }
            }
        }
    }

    // Helper for single standard hash (used for Center Crop)
    private static ulong ComputeSingleHash(SKBitmap bitmap)
    {
        ulong hash = 0;
        IntPtr pixelsAddr = bitmap.GetPixels();
        
        unsafe 
        {
            byte* ptr = (byte*)pixelsAddr;
            int rowBytes = bitmap.RowBytes;

            for (int y = 0; y < Height; y++)
            {
                byte* row = ptr + (y * rowBytes); // 8 rows
                for (int x = 0; x < Width - 1; x++) // 8 bits
                {
                    if (row[x] < row[x + 1])
                        hash |= (1UL << (y * 8 + x));
                }
            }
        }
        return hash;
    }

    // Hamming Distance: Number of different bits
    // Uses XOR and PopCount
    public static int HammingDistance(ulong a, ulong b)
    {
        ulong xor = a ^ b;
        return System.Numerics.BitOperations.PopCount(xor);
    }

    // Euclidean distance in RGB space
    public static double ColorDistance(uint c1, uint c2)
    {
        // Extract A,R,G,B components
        // We ignore Alpha difference as we are using opaque white-backed images
        
        int r1 = (int)((c1 >> 16) & 0xFF);
        int g1 = (int)((c1 >> 8) & 0xFF);
        int b1 = (int)(c1 & 0xFF);
        
        int r2 = (int)((c2 >> 16) & 0xFF);
        int g2 = (int)((c2 >> 8) & 0xFF);
        int b2 = (int)(c2 & 0xFF);
        
        // Simple Euclidean
        return Math.Sqrt(Math.Pow(r1 - r2, 2) + Math.Pow(g1 - g2, 2) + Math.Pow(b1 - b2, 2));
    }

    private static uint CalculateAverageColor(SKBitmap bitmap)
    {
        // Sample pixels to calculate average
        // For speed, sample standard 9x8 or just traverse the bitmap with step if large
        
        long rSum = 0;
        long gSum = 0;
        long bSum = 0;
        long count = 0;
        
        // Sampling step based on size to avoid reading millions of pixels
        int step = 1;
        if (bitmap.Width > 64 || bitmap.Height > 64) step = 4;
        
        int w = bitmap.Width;
        int h = bitmap.Height;
        
        IntPtr pixelsAddr = bitmap.GetPixels();
        
        unsafe 
        {
            byte* ptr = (byte*)pixelsAddr;
            int rowBytes = bitmap.RowBytes;

            for (int y = 0; y < h; y += step)
            {
                byte* row = ptr + (y * rowBytes);
                for (int x = 0; x < w; x += step)
                {
                    // BGRA (Little Endian uint) -> B is lowest byte
                    byte* p = row + x * 4;
                    byte b = p[0];
                    byte g = p[1];
                    byte r = p[2];
                    
                    // Filter out near-white background pixels to focus on the SUBJECT color
                    // Since we composited on White, background is (255,255,255).
                    // We ignore pixels that are very close to white (e.g., > 245 in all channels)
                    // This prevents small objects on large white backgrounds from washing out to "White".
                    if (r > 245 && g > 245 && b > 245)
                    {
                        continue;
                    }
                    
                    bSum += b;
                    gSum += g;
                    rSum += r;
                    count++;
                }
            }
        }
        
        if (count == 0) return 0xFFFFFFFF; // White default
        
        byte rAvg = (byte)(rSum / count);
        byte gAvg = (byte)(gSum / count);
        byte bAvg = (byte)(bSum / count);
        
        // Return 0xAARRGGBB
        return (uint)((0xFF << 24) | (rAvg << 16) | (gAvg << 8) | bAvg);
    }
}
