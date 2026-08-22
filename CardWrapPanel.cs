using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ClipboardHistory.Core;

namespace ClipboardHistory;

// 图片卡片面板：自适应「两端对齐」网格（类似 Google Photos 的 Justified 布局）。
// - 卡片按图片原始宽高比定宽定高，不裁切、不变形；
// - 行高随窗口宽度自适应：窄窗口约 2 列小图，全屏可达 4~6 列，单屏可见大量图片；
// - 非末行等比拉伸铺满整行宽度，观感整齐；超宽长图单独占满一行时等比缩小适配。
public sealed class CardWrapPanel : Panel
{
    private const double Gap = 8;
    private const double MinRowHeight = 110;  // 目标行高下限（保证窄窗口至少两列）
    private const double MaxRowHeight = 240;  // 目标行高上限（全屏时避免行过高）
    private const double MaxStretch = 1.5;    // 非末行允许的最大整行拉伸倍数
    private const double ItemMargin = 6;      // 补偿 ListBoxItem Margin=3*2

    protected override Size MeasureOverride(Size availableSize)
    {
        var rects = ComputeLayout(availableSize.Width);
        for (int i = 0; i < InternalChildren.Count && i < rects.Count; i++)
            InternalChildren[i].Measure(rects[i].Size);

        double contentH = ContentHeight(rects);
        // 视口有界且内容不足一屏时，报告视口高度：面板填满视口，
        // 子项从最上面开始排列（避免内容被 ScrollViewer 竖向居中）
        double viewH = double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height;
        return new Size(availableSize.Width, Math.Max(viewH, contentH));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rects = ComputeLayout(finalSize.Width);
        for (int i = 0; i < InternalChildren.Count && i < rects.Count; i++)
            InternalChildren[i].Arrange(rects[i]);
        return new Size(finalSize.Width, Math.Max(finalSize.Height, ContentHeight(rects)));
    }

    private static double ContentHeight(List<Rect> rects)
    {
        double h = 0;
        foreach (var r in rects) h = Math.Max(h, r.Bottom);
        return h;
    }

    // 核心布局：逐个放入当前行，放不下则把当前行等比拉伸铺满整行后换行；
    // 最后一行不拉伸（左对齐）。
    private List<Rect> ComputeLayout(double width)
    {
        var rects = new List<Rect>(InternalChildren.Count);
        int n = InternalChildren.Count;
        if (n == 0) return rects;

        double baseH = TargetRowHeight(width) + ItemMargin;
        double usable = Math.Max(160, width - 24);

        var row = new List<Size>(n >> 1);
        double y = 0;

        void Flush(bool allowStretch)
        {
            int c = row.Count;
            if (c == 0) return;
            double natural = 0, rowH = 0;
            foreach (var s in row) { natural += s.Width; rowH = Math.Max(rowH, s.Height); }

            double scale = 1.0;
            if (allowStretch)
            {
                double st = (usable - Gap * (c - 1)) / natural;
                if (st > 1.0) scale = Math.Min(st, MaxStretch);
            }

            double x = 0;
            foreach (var s in row)
            {
                var r = new Rect(x, y, Math.Round(s.Width * scale, 1), Math.Round(s.Height * scale, 1));
                rects.Add(r);
                x += r.Width + Gap;
            }
            y += Math.Round(rowH * scale, 1) + Gap;
            row.Clear();
        }

        foreach (UIElement child in InternalChildren)
        {
            Size card = CardSize(child, baseH, usable);
            double rowNatural = 0;
            foreach (var s in row) rowNatural += s.Width;
            double nextW = row.Count > 0 ? rowNatural + Gap + card.Width : card.Width;
            if (row.Count > 0 && nextW > usable)
                Flush(true);
            row.Add(card);
        }
        Flush(false);

        return rects;
    }

    // 单张卡片基准尺寸（含 ListBoxItem 边距补偿）：按条目图片的原始宽高比计算；
    // 取不到尺寸信息时回退方形；超过可用宽度（超宽长图）时整体等比缩小。
    private static Size CardSize(UIElement child, double baseH, double usable)
    {
        double aspect = 1.0;
        if (child is FrameworkElement fe && fe.DataContext is ClipEntry e)
        {
            var nat = e.GetImageDisplaySize();
            if (nat.HasValue && nat.Value.Height > 0)
                aspect = nat.Value.Width / nat.Value.Height;
        }

        double w = baseH * aspect, h = baseH;
        if (w > usable - ItemMargin)
        {
            w = usable - ItemMargin;
            h = w / aspect;
        }
        return new Size(Math.Ceiling(w) + ItemMargin, Math.Ceiling(h) + ItemMargin);
    }

    // 目标行高：窄窗口保持约两列密度，随窗口变宽而增大，封顶避免全屏行过高
    private static double TargetRowHeight(double width)
    {
        double h = (width - 48) / 3.6;
        if (h < MinRowHeight) h = MinRowHeight;
        if (h > MaxRowHeight) h = MaxRowHeight;
        return h;
    }
}
