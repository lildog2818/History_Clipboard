using System.Windows;

namespace ClipboardHistory.Core;

public sealed class ClipboardWriter
{
    private readonly ClipboardMonitor _monitor;

    public ClipboardWriter(ClipboardMonitor monitor) => _monitor = monitor;

    public void SetData(IDataObject data)
    {
        _monitor.BeginSelfWrite();
        try { Clipboard.SetDataObject(data, true); }
        catch { _monitor.EndSelfWrite(); }
    }

    public void SetText(string text)
    {
        _monitor.BeginSelfWrite();
        try { Clipboard.SetText(text); }
        catch { _monitor.EndSelfWrite(); }
    }

    public void SetImage(System.Windows.Media.Imaging.BitmapSource image)
    {
        _monitor.BeginSelfWrite();
        try { Clipboard.SetImage(image); }
        catch { _monitor.EndSelfWrite(); }
    }
}
