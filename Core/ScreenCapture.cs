using System;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ClipboardHistory.Core;

public static class ScreenCapture
{
    public static System.Drawing.Bitmap CaptureVirtualScreen(out double scale)
    {
        var vs = System.Windows.Forms.SystemInformation.VirtualScreen;
        scale = NativeMethods.GetDpiForSystem() / 96.0;
        var bmp = new System.Drawing.Bitmap(vs.Width, vs.Height);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(vs.Left, vs.Top, 0, 0, bmp.Size);
        }
        return bmp;
    }

    public static byte[] CropToPng(System.Drawing.Bitmap full, System.Drawing.Rectangle rect)
    {
        rect.Intersect(new System.Drawing.Rectangle(0, 0, full.Width, full.Height));
        if (rect.Width <= 0 || rect.Height <= 0)
            throw new ArgumentException("选区为空");
        using var crop = full.Clone(rect, full.PixelFormat);
        using var ms = new MemoryStream();
        crop.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    public static BitmapSource BitmapToSource(System.Drawing.Bitmap bmp)
    {
        IntPtr hbmp = bmp.GetHbitmap();
        try
        {
            return Imaging.CreateBitmapSourceFromHBitmap(
                hbmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            NativeMethods.DeleteObject(hbmp);
        }
    }
}
