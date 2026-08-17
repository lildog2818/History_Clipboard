using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClipboardHistory.Core;

namespace ClipboardHistory;

public partial class ScreenshotOverlay : Window
{
    private System.Drawing.Bitmap? _full;
    private double _scale = 1.0;
    private Point _start;
    private bool _dragging;
    private Rect _selection;
    private ClipEntry? _capturedEntry;

    public ScreenshotOverlay()
    {
        InitializeComponent();
    }

    public void CaptureAndShow()
    {
        try
        {
            _full?.Dispose();
            _full = ScreenCapture.CaptureVirtualScreen(out _scale);
            var vs = System.Windows.Forms.SystemInformation.VirtualScreen;
            Left = vs.Left / _scale;
            Top = vs.Top / _scale;
            Width = vs.Width / _scale;
            Height = vs.Height / _scale;

            ScreenImage.Source = ScreenCapture.BitmapToSource(_full);
            ScreenImage.Width = Width;
            ScreenImage.Height = Height;

            ResetSelection();
            Show();
            Activate();
            Focus();
        }
        catch (Exception ex)
        {
            Logger.Error("截图失败", ex);
            _full?.Dispose();
            _full = null;
        }
    }

    private void ResetSelection()
    {
        _selection = Rect.Empty;
        _dragging = false;
        _capturedEntry = null;
        Toolbar.Visibility = Visibility.Collapsed;
        SizeLabel.Visibility = Visibility.Collapsed;
        SelBorder.Visibility = Visibility.Collapsed;
        UpdateDim();
    }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(Root);
        _dragging = true;
        Toolbar.Visibility = Visibility.Collapsed;
    }

    private void Root_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var pos = e.GetPosition(Root);
        _selection = new Rect(
            Math.Min(_start.X, pos.X), Math.Min(_start.Y, pos.Y),
            Math.Abs(pos.X - _start.X), Math.Abs(pos.Y - _start.Y));
        UpdateDim();
    }

    private void Root_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        if (_selection.Width < 4 || _selection.Height < 4)
        {
            ResetSelection();
            return;
        }
        CaptureSelection();
        ShowToolbar();
    }

    private void UpdateDim()
    {
        double w = Width, h = Height;
        if (_selection.IsEmpty)
        {
            SetRect(DimTop, 0, 0, w, h);
            SetRect(DimBottom, 0, 0, 0, 0);
            SetRect(DimLeft, 0, 0, 0, 0);
            SetRect(DimRight, 0, 0, 0, 0);
            SelBorder.Visibility = Visibility.Collapsed;
            SizeLabel.Visibility = Visibility.Collapsed;
            return;
        }

        double x = _selection.X, y = _selection.Y, sw = _selection.Width, sh = _selection.Height;

        SetRect(DimTop, 0, 0, w, y);
        SetRect(DimBottom, 0, y + sh, w, h - (y + sh));
        SetRect(DimLeft, 0, y, x, sh);
        SetRect(DimRight, x + sw, y, w - (x + sw), sh);

        SelBorder.Visibility = Visibility.Visible;
        SetRect(SelBorder, x, y, sw, sh);

        SizeLabel.Visibility = Visibility.Visible;
        SizeLabel.Text = $"{(int)(sw * _scale)} × {(int)(sh * _scale)}";
        SetPos(SizeLabel, x, Math.Max(0, y - 24));
    }

    private void ShowToolbar()
    {
        Toolbar.Visibility = Visibility.Visible;
        Toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double tw = Toolbar.DesiredSize.Width, th = Toolbar.DesiredSize.Height;
        double x = _selection.X;
        double y = _selection.Y + _selection.Height + 6;
        if (y + th > Height) y = _selection.Y - th - 6;
        if (x + tw > Width) x = Width - tw - 4;
        if (x < 0) x = 0;
        if (y < 0) y = 0;
        SetPos(Toolbar, x, y);
    }

    private static void SetRect(System.Windows.Shapes.Rectangle r, double x, double y, double w, double h)
    {
        Canvas.SetLeft(r, x);
        Canvas.SetTop(r, y);
        r.Width = Math.Max(0, w);
        r.Height = Math.Max(0, h);
    }

    private static void SetPos(UIElement el, double x, double y)
    {
        Canvas.SetLeft(el, x);
        Canvas.SetTop(el, y);
    }

    private byte[] CropPng()
    {
        if (_full == null) throw new InvalidOperationException("截图未就绪");
        var rect = new System.Drawing.Rectangle(
            (int)(_selection.X * _scale), (int)(_selection.Y * _scale),
            (int)(_selection.Width * _scale), (int)(_selection.Height * _scale));
        return ScreenCapture.CropToPng(_full, rect);
    }

    // 松开鼠标即自动复制到剪贴板并入库，工具栏只保留「贴图 / 取消」
    private void CaptureSelection()
    {
        try
        {
            var png = CropPng();
            _capturedEntry = Services.Store.AddImageEntry(png);
            Services.Monitor.RememberSelfEntry(_capturedEntry);
            SetClipboardImage(_capturedEntry);
        }
        catch (Exception ex)
        {
            Logger.Error("截图保存失败", ex);
        }
    }

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        if (_capturedEntry == null) { HideOverlay(); return; }
        var win = new PinnedImageWindow(Services.Store.ResolveImage(_capturedEntry.ImageFile!));
        win.Show();
        HideOverlay();
    }

    private static void SetClipboardImage(ClipEntry entry)
    {
        var bmp = Paster.LoadBitmap(entry);
        if (bmp != null) Services.Writer.SetImage(bmp);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => HideOverlay();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape || e.Key == Key.Enter)
        {
            HideOverlay();
            e.Handled = true;
        }
    }

    // 用 Hide 而非 Close，保证窗口可被反复唤起（Close 后无法再次 Show）
    private void HideOverlay()
    {
        _full?.Dispose();
        _full = null;
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        _full?.Dispose();
        _full = null;
        base.OnClosed(e);
    }
}
