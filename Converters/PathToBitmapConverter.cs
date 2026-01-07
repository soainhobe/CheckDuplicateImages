using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace CheckDuplicate.Converters;

public class PathToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrEmpty(path))
        {
            try
            {
                // In a real app, use a thumbnail generator or cache. 
                // For now, try loading directly if it exists, or return null.
                if (System.IO.File.Exists(path))
                {
                    // Basic load. For optimization, we should decode to a smaller size, 
                    // but Avalonia's simple Bitmap(path) loads full image. 
                    // To avoid OOM on large folders, proper thumbnailing is needed.
                    // For this task demo, we will check if it's an image and valid path.
                    // Note: This blocks UI thread if many images load. 
                    return new Bitmap(path);
                }
            }
            catch
            {
                // Return generic icon or null on failure
            }
        }
        return null; // Fallback to default content in view
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
