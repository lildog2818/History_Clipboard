using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using ClipboardHistory.Core;

namespace ClipboardHistory;

public sealed class ImagePathConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string rel && !string.IsNullOrEmpty(rel))
        {
            try
            {
                var dataDir = Path.GetDirectoryName(ClipboardStore.ImagesDirectory) ?? "";
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(Path.Combine(dataDir, rel), UriKind.Absolute);
                bmp.DecodePixelWidth = 48;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { }
        }
        return Binding.DoNothing;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
