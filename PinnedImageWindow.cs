using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ClipboardHistory.Core;

namespace ClipboardHistory;

public partial class PinnedImageWindow : Window
{
    private static readonly List<PinnedImageWindow> OpenWindows = new();

    private Point _dragStart;
    private double _baseWidth;
    private double _baseHeight;
    private double _zoom = 1.0;

    public PinnedImageWindow(string imagePath)
    {
        InitializeComponent();

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri(imagePath, UriKind.Absolute);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        Img.Source = bmp;

        double w = bmp.PixelWidth, h = bmp.PixelHeight;
        var vs = System.Windows.Forms.SystemInformation.WorkingArea;
        double maxW = vs.Width * 0.8, maxH = vs.Height * 0.8;
        double fit = Math.Min(1.0, Math.Min(maxW / w, maxH / h));
        _baseWidth = w * fit;
        _baseHeight = h * fit;
        Width = _baseWidth;
        Height = _baseHeight;
        Left = vs.Left + (vs.Width - Width) / 2;
        Top = vs.Top + (vs.Height - Height) / 2;

        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
        MouseWheel += OnMouseWheel;

        OpenWindows.Add(this);
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
        => CloseBtn.Visibility = Visibility.Visible;

    private void Window_MouseLeave(object sender, MouseEventArgs e)
        => CloseBtn.Visibility = Visibility.Collapsed;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var pos = e.GetPosition(this);
            Left += pos.X - _dragStart.X;
            Top += pos.Y - _dragStart.Y;
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        double factor = e.Delta > 0 ? 1.1 : 1 / 1.1;
        _zoom = Math.Clamp(_zoom * factor, 0.1, 6.0);
        Width = _baseWidth * _zoom;
        Height = _baseHeight * _zoom;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        Close();
        base.OnMouseDoubleClick(e);
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();

        var copy = new MenuItem { Header = "复制图片" };
        copy.Click += (_, _) =>
        {
            if (Img.Source is BitmapSource bmp) Services.Writer.SetImage(bmp);
        };
        var close = new MenuItem { Header = "关闭" };
        close.Click += (_, _) => Close();
        var closeAll = new MenuItem { Header = "全部关闭" };
        closeAll.Click += (_, _) => CloseAll();

        menu.Items.Add(copy);
        menu.Items.Add(close);
        menu.Items.Add(closeAll);
        menu.IsOpen = true;
        base.OnMouseRightButtonUp(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        OpenWindows.Remove(this);
        base.OnClosed(e);
    }

    public static void CloseAll()
    {
        foreach (var w in OpenWindows.ToArray()) w.Close();
    }
}
