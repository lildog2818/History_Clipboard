using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ClipboardHistory.Core;

namespace ClipboardHistory;

public partial class ScreenshotOverlay : Window
{
    private System.Drawing.Bitmap? _full;
    private double _scale = 1.0;
    private Point _start;
    private bool _dragging;
    private Rect _selection;

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
        catch
        {
            _full?.Dispose();
            _full = null;
        }
    }

    private void ResetSelection()
    {
        _selection = Rect.Empty;
        _dragging = false;
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

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var png = CropPng();
            var entry = Services.Store.AddImageEntry(png);
            SetClipboardImage(entry);
            Close();
        }
        catch { }
    }

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var png = CropPng();
            var entry = Services.Store.AddImageEntry(png);
            SetClipboardImage(entry);
            var win = new PinnedImageWindow(Services.Store.ResolveImage(entry.ImageFile!));
            win.Show();
            Close();
        }
        catch { }
    }

    private async void Ocr_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var png = CropPng();
            var entry = Services.Store.AddImageEntry(png);
            SetClipboardImage(entry);
            Close();

            var text = await OcrService.RecognizeAsync(png);
            if (!string.IsNullOrEmpty(text)) Services.Store.SetOcrText(entry.Id, text);
            ShowOcrResult(text);
        }
        catch { }
    }

    private static void SetClipboardImage(ClipEntry entry)
    {
        var bmp = Paster.LoadBitmap(entry);
        if (bmp != null) Services.Writer.SetImage(bmp);
    }

    private static void ShowOcrResult(string text)
    {
        var win = new Window
        {
            Title = "OCR 结果",
            Width = 520,
            Height = 320,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true
        };
        var grid = new Grid { Margin = new Thickness(12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var box = new TextBox
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            IsReadOnly = true
        };
        Grid.SetRow(box, 0);
        var btn = new Button
        {
            Content = "复制文字",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(14, 5, 14, 5)
        };
        btn.Click += (_, _) =>
        {
            Services.Writer.SetText(text);
            win.Close();
        };
        Grid.SetRow(btn, 1);
        grid.Children.Add(box);
        grid.Children.Add(btn);
        win.Content = grid;
        win.Show();
        box.Focus();
        box.SelectAll();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && !_selection.IsEmpty)
        {
            Copy_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _full?.Dispose();
        _full = null;
        base.OnClosed(e);
    }
}
