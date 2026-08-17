using System;
using System.Globalization;
using System.Windows.Data;

namespace ClipboardHistory;

// 图片卡片自适应宽度：按网格宽度算出能铺满一行的卡片尺寸
public sealed class CardWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double width = value is double d ? d : 0;
        double usable = width - 24;
        if (usable < 160) usable = 160;
        int columns = Math.Max(2, (int)(usable / 180));
        return (usable - columns * 8) / columns;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
