using System.Windows;

namespace ClipboardHistory.Core;

public sealed class ClipboardWriter
{
    private readonly ClipboardMonitor _monitor;

    public ClipboardWriter(ClipboardMonitor monitor) => _monitor = monitor;

    public void SetData(IDataObject data, bool copy = true)
    {
        _monitor.BeginSelfWrite(TextHashOf(data));
        try { Clipboard.SetDataObject(data, copy); }
        finally { _monitor.EndSelfWrite(); }
    }

    public void SetText(string text)
    {
        _monitor.BeginSelfWrite(Hash.OfText(text));
        try { Clipboard.SetText(text); }
        finally { _monitor.EndSelfWrite(); }
    }

    public void SetImage(System.Windows.Media.Imaging.BitmapSource image)
    {
        _monitor.BeginSelfWrite();
        try { Clipboard.SetImage(image); }
        finally { _monitor.EndSelfWrite(); }
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
