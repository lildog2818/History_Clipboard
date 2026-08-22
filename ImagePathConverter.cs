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
                // 解码到 800px 宽：网格卡片最大约 400+ DIP，配合高 DPI 仍清晰；
                // 同时控制内存（完整解码 4K 截图单张约需 30MB）
                bmp.DecodePixelWidth = 800;
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
