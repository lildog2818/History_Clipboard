using System.Windows;

namespace ClipboardHistory.Core;

public sealed class ClipboardWriter
{
    private readonly ClipboardMonitor _monitor;

    public ClipboardWriter(ClipboardMonitor monitor) => _monitor = monitor;

    // copy=true 立即拷贝数据（复制模式，安全）；copy=false 延迟渲染（粘贴模式，更快）
    public void SetData(IDataObject data, bool copy = true)
    {
        _monitor.BeginSelfWrite();
        try { Clipboard.SetDataObject(data, copy); }
        finally { _monitor.EndSelfWrite(); }
    }

    public void SetText(string text)
    {
        _monitor.BeginSelfWrite();
        try { Clipboard.SetText(text); }
        finally { _monitor.EndSelfWrite(); }
    }

    public void SetImage(System.Windows.Media.Imaging.BitmapSource image)
    {
        _monitor.BeginSelfWrite();
        try { Clipboard.SetImage(image); }
        finally { _monitor.EndSelfWrite(); }
    }
}
