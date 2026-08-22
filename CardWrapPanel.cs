using System;
using System.Windows;
using System.Windows.Controls;
using ClipboardHistory.Core;

namespace ClipboardHistory;

// 图片卡片面板（替代 WrapPanel），两种布局按面板宽度自动切换：
// - 常规窗口（宽 < ActualSizeMinWidth）：每行固定 2 张方形缩略图（原行为）。
// - 大窗口 / 最大化（全屏浏览）：每张卡片按图片原始尺寸显示（等比，
//   超出视口宽度时等比缩小以适配），逐行流式排布，行高随内容变化。
public sealed class CardWrapPanel : Panel
{
    private const double Gap = 8;
    // 面板可用宽度达到该值即切换为原图尺寸布局（最大化/全屏时必然超过）
    public const double ActualSizeMinWidth = 720;
    // 补偿 ListBoxItem Margin=3*2，保证图片绘制区域等于原始尺寸
    private const double ItemMargin = 6;

    protected override Size MeasureOverride(Size availableSize)
    {
        bool actual = availableSize.Width >= ActualSizeMinWidth;
        double thumb = CardSize(availableSize.Width);
        double contentH = 0;

        if (!actual)
        {
            foreach (UIElement child in InternalChildren)
                child.Measure(new Size(thumb, thumb));

            int count = InternalChildren.Count;
            int rows = Math.Max(1, (count + 1) / 2);
            contentH = rows * thumb + Math.Max(0, rows - 1) * Gap;
        }
        else
        {
            double x = 0, y = 0, rowH = 0;
            foreach (UIElement child in InternalChildren)
            {
                Size s = ChildDisplaySize(child, availableSize.Width);
                child.Measure(s);
                if (x > 0 && x + s.Width > availableSize.Width)
                {
                    x = 0;
                    y += rowH + Gap;
                    rowH = 0;
                }
                x += s.Width + Gap;
                rowH = Math.Max(rowH, s.Height);
            }
            contentH = y + rowH;
        }

        // 视口有界且内容不足一屏时，报告视口高度：面板填满视口，
        // 子项从最上面开始排列（避免内容被 ScrollViewer 竖向居中）
        double viewH = double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height;
        return new Size(availableSize.Width, Math.Max(viewH, contentH));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        bool actual = finalSize.Width >= ActualSizeMinWidth;
        double thumb = CardSize(finalSize.Width);

        if (!actual)
        {
            double x = 0, y = 0;
            int i = 0;
            foreach (UIElement child in InternalChildren)
            {
                if (i % 2 == 0)
                {
                    x = 0;
                    if (i > 0) y += thumb + Gap;
                }
                else
                {
                    x = thumb + Gap;
                }
                child.Arrange(new Rect(x, y, thumb, thumb));
                i++;
            }
            int count = InternalChildren.Count;
            int rows = Math.Max(1, (count + 1) / 2);
            double contentH = rows * thumb + Math.Max(0, rows - 1) * Gap;
            return new Size(finalSize.Width, Math.Max(finalSize.Height, contentH));
        }

        // 原图尺寸模式：流式排布，放不下则换行
        double ax = 0, ay = 0, aRowH = 0;
        foreach (UIElement child in InternalChildren)
        {
            Size s = ChildDisplaySize(child, finalSize.Width);
            if (ax > 0 && ax + s.Width > finalSize.Width)
            {
                ax = 0;
                ay += aRowH + Gap;
                aRowH = 0;
            }
            child.Arrange(new Rect(ax, ay, s.Width, s.Height));
            ax += s.Width + Gap;
            aRowH = Math.Max(aRowH, s.Height);
        }
        return new Size(finalSize.Width, Math.Max(finalSize.Height, ay + aRowH));
    }

    // 单张卡片的显示尺寸：优先取条目图片的原始尺寸（DIP），
    // 超过面板可用宽度时等比缩小；取不到时回退为方形缩略图。
    private Size ChildDisplaySize(UIElement child, double panelWidth)
    {
        double fallback = CardSize(panelWidth);
        if (child is FrameworkElement fe && fe.DataContext is ClipEntry e)
        {
            var nat = e.GetImageDisplaySize();
            if (nat.HasValue)
            {
                double usable = Math.Max(160, panelWidth - 24);
                double w = nat.Value.Width, h = nat.Value.Height;
                if (w > usable)
                {
                    h *= usable / w;
                    w = usable;
                }
                return new Size(Math.Ceiling(w) + ItemMargin, Math.Ceiling(h) + ItemMargin);
            }
        }
        return new Size(fallback, fallback);
    }

    // 与原来的 CardWidthConverter 相同的公式：按面板宽度算出铺满一行的缩略图尺寸
    private static double CardSize(double width)
    {
        double usable = width - 24;
        if (usable < 120) usable = 120;
        return (usable - 16) / 2;
    }
}
