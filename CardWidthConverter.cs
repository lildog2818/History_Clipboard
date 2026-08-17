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
        if (usable < 120) usable = 120;
        return (usable - 16) / 2; // 每行固定 2 张大图，铺满整行
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
