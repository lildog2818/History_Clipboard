using System;
using System.Windows;
using System.Windows.Controls;

namespace ClipboardHistory;

// 图片卡片面板（替代 WrapPanel）：
// 每行固定 2 张方形大图，卡片尺寸在 Measure/Arrange 时直接按面板宽度计算。
// 不依赖 ActualWidth 绑定——页签切换（折叠→显示）时也能立刻得到正确尺寸，
// 不会出现"切到图片页后卡片卡在极小尺寸"的问题。
public sealed class CardWrapPanel : Panel
{
    private const double Gap = 8;

    protected override Size MeasureOverride(Size availableSize)
    {
        double card = CardSize(availableSize.Width);
        foreach (UIElement child in InternalChildren)
            child.Measure(new Size(card, card));

        int count = InternalChildren.Count;
        int rows = Math.Max(1, (count + 1) / 2);
        double contentH = rows * card + (rows - 1) * Gap;

        // 视口有界且内容不足一屏时，报告视口高度：面板填满视口，
        // 子项从最上面开始排列（避免内容被 ScrollViewer 竖向居中）
        double viewH = double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height;
        return new Size(availableSize.Width, Math.Max(viewH, contentH));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double card = CardSize(finalSize.Width);
        double x = 0, y = 0;
        int i = 0;
        foreach (UIElement child in InternalChildren)
        {
            if (i % 2 == 0)
            {
                x = 0;
                if (i > 0) y += card + Gap;
            }
            else
            {
                x = card + Gap;
            }
            child.Arrange(new Rect(x, y, card, card));
            i++;
        }
        int count = InternalChildren.Count;
        int rows = Math.Max(1, (count + 1) / 2);
        double contentH = rows * card + (rows - 1) * Gap;
        return new Size(finalSize.Width, Math.Max(finalSize.Height, contentH));
    }

    // 与原来的 CardWidthConverter 相同的公式：按面板宽度算出铺满一行的卡片尺寸
    private static double CardSize(double width)
    {
        double usable = width - 24;
        if (usable < 120) usable = 120;
        return (usable - 16) / 2;
    }
}
