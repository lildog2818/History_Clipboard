using System;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ClipboardHistory.Core;

public sealed class ClipboardWriter
{
    private readonly ClipboardMonitor _monitor;

    public ClipboardWriter(ClipboardMonitor monitor) => _monitor = monitor;

    // 一律使用 copy=false（延迟渲染）：copy=true 会调用 Flush→OpenClipboard，
    // 在本机/某些环境下会间歇性失败(CLIPBRD_E_CANT_OPEN)。带重试兜底。
    public void SetData(IDataObject data)
    {
        _monitor.BeginSelfWrite(TextHashOf(data));
        try { SetWithRetry(() => Clipboard.SetDataObject(data, false)); }
        finally { _monitor.EndSelfWrite(); }
    }

    public void SetText(string text)
    {
        var d = new DataObject();
        d.SetData(DataFormats.UnicodeText, text);
        _monitor.BeginSelfWrite(Hash.OfText(text));
        try { SetWithRetry(() => Clipboard.SetDataObject(d, false)); }
        finally { _monitor.EndSelfWrite(); }
    }

    public void SetImage(BitmapSource image)
    {
        var d = new DataObject();
        d.SetData(DataFormats.Bitmap, image);
        _monitor.BeginSelfWrite();
        try { SetWithRetry(() => Clipboard.SetDataObject(d, false)); }
        finally { _monitor.EndSelfWrite(); }
    }

    private static void SetWithRetry(Action action)
    {
        Exception? last = null;
        for (int i = 0; i < 4; i++)
        {
            try { action(); return; }
            catch (Exception ex)
            {
                last = ex;
                Thread.Sleep(120);
            }
        }
        if (last != null) throw last;
    }

    private static string? TextHashOf(IDataObject data)
    {
        try
        {
            if (data.GetDataPresent(DataFormats.UnicodeText))
                return Hash.OfText(data.GetData(DataFormats.UnicodeText) as string ?? "");
        }
        catch { }
        return null;
    }
}
