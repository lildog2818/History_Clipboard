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
    private bool _selfWritePending;
    private string _selfWriteHash = "";
    private ClipEntry? _selfEntry;

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

    // 自写剪贴板守卫：
    //  WM_CLIPBOARDUPDATE 是异步投递的（SetData 返回后才在 UI 线程收到），
    //  因此不能靠“写入完成后立刻结束时间窗”（那会让图片/文件的自写更新漏网）。
    //  方案：
    //   1) 消费“下一次”剪贴板更新消息并忽略（5s 窗口内到达才算自写）；
    //   2) 内容比对（文本哈希/文件列表/图片哈希+像素）作双保险；
    //   3) 写入失败时调用 CancelSelfWrite，避免误吞用户下一次真实复制。
    public void BeginSelfWrite(string? contentHash = null)
    {
        _selfWritePending = true;
        _selfWriteUntilTicks = DateTime.UtcNow.AddSeconds(5).Ticks;
        if (!string.IsNullOrEmpty(contentHash)) _selfWriteHash = contentHash;
    }

    public void EndSelfWrite()
    {
        // 更新消息此刻可能尚未到达，不在此清除守卫；由下一次 WM_CLIPBOARDUPDATE 消费。
    }

    public void CancelSelfWrite()
    {
        _selfWritePending = false;
        _selfWriteUntilTicks = 0;
        _selfEntry = null;
        _selfWriteHash = "";
    }

    // 记录本次自写对应的原条目，用于捕获时按内容比对（文本/文件列表/图片像素）
    public void RememberSelfEntry(ClipEntry entry) => _selfEntry = entry;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            if (_selfWritePending)
            {
                _selfWritePending = false;
                if (DateTime.UtcNow.Ticks < _selfWriteUntilTicks)
                    return IntPtr.Zero; // 消费本次自写产生的更新，忽略
                // 超时后才到达：交给下方内容比对兜底
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
                        if (IsSelfWrite(entry)) return; // 自写内容（文本/文件/图片）
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

    // 捕获内容是否等于最近一次自写（时间窗内）：文本哈希 / 文件列表 / 图片哈希+像素
    private bool IsSelfWrite(ClipEntry captured)
    {
        if (DateTime.UtcNow.Ticks >= _selfWriteUntilTicks) return false;
        if (!string.IsNullOrEmpty(_selfWriteHash) && captured.Hash == _selfWriteHash) return true;
        var self = _selfEntry;
        if (self == null) return false;

        if (captured.IsFileList && self.IsFileList)
            return captured.Files.SequenceEqual(self.Files);
        if (captured.IsImage && self.IsImage)
            return captured.Hash == self.Hash || ImagesEqual(self, captured);
        return captured.PlainText == self.PlainText;
    }

    private static bool ImagesEqual(ClipEntry a, ClipEntry b)
    {
        try
        {
            var pa = DecodePixels(Services.Store.ResolveImage(a.ImageFile!));
            var pb = DecodePixels(Services.Store.ResolveImage(b.ImageFile!));
            return pa != null && pb != null && pa.SequenceEqual(pb);
        }
        catch { return false; }
    }

    private static byte[]? DecodePixels(string path)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri(path, UriKind.Absolute);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        int stride = bmp.PixelWidth * 4;
        var pixels = new byte[stride * bmp.PixelHeight];
        bmp.CopyPixels(pixels, stride, 0);
        return pixels;
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
