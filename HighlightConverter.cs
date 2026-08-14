using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace ClipboardHistory;

public sealed class HighlightConverter : IValueConverter
{
    public static string CurrentQuery { get; set; } = "";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string ?? "";
        var tb = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        tb.Foreground = (Brush)Application.Current.TryFindResource("ForegroundBrush") ?? Brushes.Black;

        var q = (CurrentQuery ?? "").Trim();
        if (q.Length == 0)
        {
            tb.Text = text;
            return tb;
        }

        var accent = (Brush)Application.Current.TryFindResource("AccentBrush") ?? Brushes.DodgerBlue;
        int idx = 0;
        while (idx < text.Length)
        {
            int pos = text.IndexOf(q, idx, StringComparison.OrdinalIgnoreCase);
            if (pos < 0)
            {
                tb.Inlines.Add(new Run(text[idx..]));
                break;
            }
            if (pos > idx) tb.Inlines.Add(new Run(text[idx..pos]));
            tb.Inlines.Add(new Run(text.Substring(pos, q.Length))
            {
                Foreground = accent,
                FontWeight = FontWeights.Bold
            });
            idx = pos + q.Length;
        }
        return tb;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
