using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ClipboardHistory.Core;

public sealed class ClipboardMonitor : IDisposable
{
    private readonly ClipboardStore _store;
    private HwndSource? _source;
    private long _selfWriteUntilTicks;

    public event Action<ClipEntry>? EntryAdded;

    public ClipboardMonitor(ClipboardStore store) => _store = store;

    public void Attach()
    {
        var parameters = new HwndSourceParameters("ClipboardHistoryMonitor")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ExtendedWindowStyle = 0
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
        NativeMethods.AddClipboardFormatListener(_source.Handle);
    }

    // 自写剪贴板：用时间窗口忽略后续所有通知（一次写入可能触发多次 WM_CLIPBOARDUPDATE）
    public void BeginSelfWrite() => _selfWriteUntilTicks = DateTime.UtcNow.AddMilliseconds(800).Ticks;
    public void EndSelfWrite() => _selfWriteUntilTicks = 0;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            if (DateTime.UtcNow.Ticks < _selfWriteUntilTicks)
                return IntPtr.Zero; // 忽略自写
            CaptureWithRetry();
        }
        return IntPtr.Zero;
    }

    private void CaptureWithRetry()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var data = Clipboard.GetDataObject();
                if (data != null)
                {
                    var entry = BuildEntry(data);
                    if (entry != null)
                    {
                        if (entry.Hash == _store.LatestHash) return; // consecutive duplicate
                        _store.Add(entry);
                        EntryAdded?.Invoke(entry);
                    }
                }
                return;
            }
            catch (ExternalException) { Thread.Sleep(15); }
            catch (Exception ex)
            {
                Logger.Error("读取剪贴板失败", ex);
                return;
            }
        }
    }

    private ClipEntry? BuildEntry(IDataObject data)
    {
        var entry = new ClipEntry();
        FillSource(entry);
        if (IsExcluded(entry.SourceApp)) return null;

        if (data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = data.GetData(DataFormats.FileDrop) as string[];
            if (files is { Length: > 0 })
            {
                entry.Files = files;
                entry.PlainText = string.Join("\r\n", files);
                entry.Hash = Hash.OfText(string.Join("|", files));
                return entry;
            }
        }

        if (data.GetDataPresent(DataFormats.Bitmap))
        {
            var src = GetBitmap(data);
            if (src != null)
            {
                var pngBytes = EncodePng(src);
                if (pngBytes != null)
                {
                    var hash = Hash.OfBytes(pngBytes);
                    if (hash == _store.LatestHash) return null; // consecutive duplicate
                    var rel = _store.SaveImagePng(pngBytes, entry.Id);
                    if (rel != null)
                    {
                        entry.ImageFile = rel;
                        entry.Hash = hash;
                        return entry;
                    }
                }
            }
        }

        var text = GetText(data);
        if (string.IsNullOrEmpty(text)) return null;

        entry.PlainText = text;
        entry.Hash = Hash.OfText(text);
        if (!Services.Settings.Current.PlainTextOnly)
        {
            if (data.GetDataPresent(DataFormats.Html))
                entry.Html = Limit(data.GetData(DataFormats.Html) as string, 2_000_000);
            if (data.GetDataPresent(DataFormats.Rtf))
                entry.Rtf = Limit(data.GetData(DataFormats.Rtf) as string, 2_000_000);
        }
        return entry;
    }

    private static bool IsExcluded(string app)
    {
        if (string.IsNullOrEmpty(app)) return false;
        var list = Services.Settings?.Current?.ExcludedApps;
        if (list == null || list.Length == 0) return false;
        return list.Any(x => string.Equals(x, app, StringComparison.OrdinalIgnoreCase));
    }

    private static BitmapSource? GetBitmap(IDataObject data)
    {
        var obj = data.GetData(DataFormats.Bitmap);
        if (obj is BitmapSource src) return src;
        if (obj is System.Drawing.Bitmap gdi) return ScreenCapture.BitmapToSource(gdi);
        return null;
    }

    private static byte[]? EncodePng(BitmapSource src)
    {
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(src));
            using var ms = new System.IO.MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            Logger.Error("图片编码失败", ex);
            return null;
        }
    }

    private static string GetText(IDataObject data)
    {
        if (data.GetDataPresent(DataFormats.UnicodeText))
            return data.GetData(DataFormats.UnicodeText) as string ?? "";
        if (data.GetDataPresent(DataFormats.Text))
            return data.GetData(DataFormats.Text) as string ?? "";
        return "";
    }

    private static string? Limit(string? s, int max) =>
        s == null ? null : (s.Length > max ? s[..max] : s);

    private static void FillSource(ClipEntry entry)
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            var sb = new System.Text.StringBuilder(512);
            NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
            entry.SourceTitle = sb.ToString();
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            try
            {
                using var proc = Process.GetProcessById((int)pid);
                entry.SourceApp = proc.ProcessName;
            }
            catch { entry.SourceApp = ""; }
        }
        catch { }
    }

    public void Dispose()
    {
        if (_source != null)
        {
            NativeMethods.RemoveClipboardFormatListener(_source.Handle);
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }
    }
}
