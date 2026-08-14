using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ClipboardHistory.Core;

public sealed class ClipboardMonitor : IDisposable
{
    private readonly ClipboardStore _store;
    private HwndSource? _source;
    private int _selfWriteCount;

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

    public void BeginSelfWrite() => _selfWriteCount++;
    public void EndSelfWrite() { if (_selfWriteCount > 0) _selfWriteCount--; }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            if (_selfWriteCount > 0)
            {
                _selfWriteCount--;
                return IntPtr.Zero;
            }
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
                        if (entry.IsImage) TriggerOcr(entry);
                    }
                }
                return;
            }
            catch (ExternalException) { Thread.Sleep(15); }
            catch { return; }
        }
    }

    private void TriggerOcr(ClipEntry entry)
    {
        var path = _store.ResolveImage(entry.ImageFile!);
        _ = Task.Run(async () =>
        {
            try
            {
                var bytes = System.IO.File.ReadAllBytes(path);
                var text = await OcrService.RecognizeAsync(bytes);
                if (!string.IsNullOrEmpty(text))
                    _store.SetOcrText(entry.Id, text);
            }
            catch { }
        });
    }

    private ClipEntry? BuildEntry(IDataObject data)
    {
        var entry = new ClipEntry();

        if (data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = data.GetData(DataFormats.FileDrop) as string[];
            if (files is { Length: > 0 })
            {
                entry.Files = files;
                entry.PlainText = string.Join("\r\n", files);
                entry.Hash = Hash.OfText(string.Join("|", files));
                FillSource(entry);
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
                        FillSource(entry);
                        return entry;
                    }
                }
            }
        }

        var text = GetText(data);
        if (string.IsNullOrEmpty(text)) return null;

        entry.PlainText = text;
        entry.Hash = Hash.OfText(text);
        if (data.GetDataPresent(DataFormats.Html))
            entry.Html = Limit(data.GetData(DataFormats.Html) as string, 2_000_000);
        if (data.GetDataPresent(DataFormats.Rtf))
            entry.Rtf = Limit(data.GetData(DataFormats.Rtf) as string, 2_000_000);
        FillSource(entry);
        return entry;
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
        catch { return null; }
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
