using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;

namespace ClipboardHistory.Core;

public sealed class ClipEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string PlainText { get; set; } = "";
    public string? Html { get; set; }
    public string? Rtf { get; set; }
    public string? ImageFile { get; set; }
    public string[] Files { get; set; } = Array.Empty<string>();
    public string SourceApp { get; set; } = "";
    public string SourceTitle { get; set; } = "";
    public bool Pinned { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string Note { get; set; } = "";
    public string Hash { get; set; } = "";

    public bool IsImage => !string.IsNullOrEmpty(ImageFile);
    public bool IsFileList => Files is { Length: > 0 };
    public bool IsRich => !string.IsNullOrEmpty(Html) || !string.IsNullOrEmpty(Rtf);

    private static readonly ConcurrentDictionary<string, (double W, double H)> ImageSizeCache = new();

    // 图片原始显示尺寸（WPF 设备无关单位，已按图片自身 DPI 换算），按文件路径缓存。
    // 供 CardWrapPanel 在大窗口/全屏模式下按原图尺寸排布卡片；加载失败返回 null。
    public (double Width, double Height)? GetImageDisplaySize()
    {
        if (string.IsNullOrEmpty(ImageFile)) return null;
        var dir = Path.GetDirectoryName(ClipboardStore.ImagesDirectory);
        if (string.IsNullOrEmpty(dir)) return null;
        var full = Path.Combine(dir, ImageFile);
        var s = ImageSizeCache.GetOrAdd(full, static p =>
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(p, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                double dx = bmp.DpiX > 0 ? bmp.DpiX : 96;
                double dy = bmp.DpiY > 0 ? bmp.DpiY : 96;
                return (bmp.PixelWidth * 96.0 / dx, bmp.PixelHeight * 96.0 / dy);
            }
            catch { return (0d, 0d); }
        });
        return s.W > 0 && s.H > 0 ? s : null;
    }

    public string TypeBadge =>
        IsFileList ? "文件" : IsImage ? "图片" : IsRich ? "富文本" : "文本";

    public string PinText => Pinned ? "📌" : "";

    public string DisplayText
    {
        get
        {
            if (IsFileList)
                return $"{Files.Length} 个文件 · {Files[0]}";
            if (IsImage)
                return "(图片)";
            var t = PlainText ?? "";
            t = t.Replace('\r', ' ').Replace('\n', ' ');
            return t.Length > 200 ? t[..200] + "…" : t;
        }
    }
}

public sealed class HotkeySetting
{
    public uint Modifiers { get; set; }
    public uint Key { get; set; }
}

public sealed class Settings
{
    public string DataDirectory { get; set; } = "";
    public HotkeySetting SearchHotkey { get; set; } = new() { Modifiers = 0x0002, Key = 0xC0 };
    public HotkeySetting ScreenshotHotkey { get; set; } = new() { Modifiers = 0x0003, Key = 0x41 };
    public string Theme { get; set; } = "system";
    public bool RestoreClipboardAfterPaste { get; set; } = true;
    public bool QuickPasteNumberKeys { get; set; } = true;
    public bool AutoStart { get; set; }
    public bool FirstRun { get; set; } = true;
    public int MaxEntries { get; set; }
    public bool PlainTextOnly { get; set; }
    public string[] ExcludedApps { get; set; } = Array.Empty<string>();
    public double WindowX { get; set; }
    public double WindowY { get; set; }
    public double WindowW { get; set; }
    public double WindowH { get; set; }
}

public static class Hash
{
    public static string OfText(string s) => OfBytes(Encoding.UTF8.GetBytes(s));

    public static string OfBytes(byte[] data)
    {
        var hash = SHA256.HashData(data);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
