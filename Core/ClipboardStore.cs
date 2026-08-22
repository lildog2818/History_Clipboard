using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows.Media.Imaging;

namespace ClipboardHistory.Core;

public sealed class ClipboardStore : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SettingsStore _settings;
    private readonly List<ClipEntry> _entries = new();
    private readonly object _lock = new();
    private string _dataDir;
    private string _imagesDir;
    private Timer? _saveTimer;

    public static string ImagesDirectory { get; private set; } = "";

    public event Action? Changed;

    public ClipboardStore(SettingsStore settings)
    {
        _settings = settings;
        _dataDir = Path.GetFullPath(settings.Current.DataDirectory);
        _imagesDir = Path.Combine(_dataDir, "images");
        Directory.CreateDirectory(_dataDir);
        Directory.CreateDirectory(_imagesDir);
        ImagesDirectory = _imagesDir;
        Load();
    }

    public string DataDirectory => _dataDir;
    public string HistoryFile => Path.Combine(_dataDir, "history.json");

    public IReadOnlyList<ClipEntry> Entries
    {
        get { lock (_lock) return _entries.ToList(); }
    }

    public string? LatestHash
    {
        get { lock (_lock) return _entries.Count > 0 ? _entries[0].Hash : null; }
    }

    public void Add(ClipEntry entry)
    {
        lock (_lock)
        {
            _entries.Insert(0, entry);
            TrimToLimitLocked();
        }
        ScheduleSave();
        Changed?.Invoke();
    }

    private void TrimToLimitLocked()
    {
        int max = _settings.Current.MaxEntries;
        if (max <= 0) return;
        var overflow = _entries.Where(e => !e.Pinned).Skip(max).ToList();
        foreach (var e in overflow)
        {
            if (e.ImageFile != null) TryDeleteFile(ResolveImage(e.ImageFile));
            _entries.Remove(e);
        }
    }

    public ClipEntry AddImageEntry(byte[] pngBytes)
    {
        var id = Guid.NewGuid();
        var rel = SaveImagePng(pngBytes, id);
        if (rel == null) throw new InvalidOperationException("无法保存图片");
        var entry = new ClipEntry
        {
            Id = id,
            ImageFile = rel,
            Hash = Hash.OfBytes(pngBytes),
            PlainText = "",
            SourceApp = "截图",
            SourceTitle = "截图"
        };
        Add(entry);
        return entry;
    }

    // 删除 = 永久删除：图片文件直接物理删除（不进回收站），
    // 并立即同步写盘 history.json（不走 500ms 延迟保存，
    // 避免删除后程序立刻退出时旧数据未落盘导致条目"复活"）
    public void Remove(Guid id)
    {
        bool removed = false;
        lock (_lock)
        {
            var e = _entries.FirstOrDefault(x => x.Id == id);
            if (e == null) return;
            if (e.ImageFile != null) TryDeleteFile(ResolveImage(e.ImageFile));
            _entries.Remove(e);
            removed = true;
        }
        if (removed) SaveNow();
        Changed?.Invoke();
    }

    public void Clear(bool keepPinned)
    {
        lock (_lock)
        {
            var toRemove = keepPinned ? _entries.Where(e => !e.Pinned).ToList() : _entries.ToList();
            foreach (var e in toRemove)
            {
                if (e.ImageFile != null) TryDeleteFile(ResolveImage(e.ImageFile));
                _entries.Remove(e);
            }
        }
        SaveNow();
        Changed?.Invoke();
    }

    public void TogglePin(Guid id)
    {
        lock (_lock)
        {
            var e = _entries.FirstOrDefault(x => x.Id == id);
            if (e != null) e.Pinned = !e.Pinned;
        }
        ScheduleSave();
        Changed?.Invoke();
    }

    public ClipEntry? Find(Guid id)
    {
        lock (_lock) return _entries.FirstOrDefault(x => x.Id == id);
    }

    public void UpdateMeta(Guid id, string note, string[] tags)
    {
        lock (_lock)
        {
            var e = _entries.FirstOrDefault(x => x.Id == id);
            if (e == null) return;
            e.Note = note ?? "";
            e.Tags = tags ?? Array.Empty<string>();
        }
        ScheduleSave();
        Changed?.Invoke();
    }

    public string? SaveImagePng(byte[] pngBytes, Guid id)
    {
        try
        {
            var rel = Path.Combine("images", id.ToString("N") + ".png");
            File.WriteAllBytes(Path.Combine(_dataDir, rel), pngBytes);
            return rel;
        }
        catch { return null; }
    }

    public string? SaveImage(BitmapSource image, Guid id)
    {
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return SaveImagePng(ms.ToArray(), id);
        }
        catch { return null; }
    }

    public string ResolveImage(string rel) => Path.Combine(_dataDir, rel);

    public bool ChangeDataDirectory(string newDir)
    {
        newDir = Path.GetFullPath(newDir);
        if (string.Equals(newDir, _dataDir, StringComparison.OrdinalIgnoreCase)) return true;
        try
        {
            Directory.CreateDirectory(newDir);
            var newImages = Path.Combine(newDir, "images");
            Directory.CreateDirectory(newImages);

            SaveNow();

            MoveDirectoryContents(_imagesDir, newImages);
            var histFile = Path.Combine(_dataDir, "history.json");
            if (File.Exists(histFile))
                File.Move(histFile, Path.Combine(newDir, "history.json"), true);

            _dataDir = newDir;
            _imagesDir = newImages;
            ImagesDirectory = newImages;

            _settings.Current.DataDirectory = newDir;
            _settings.Save();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void MoveDirectoryContents(string srcDir, string dstDir)
    {
        if (!Directory.Exists(srcDir)) return;
        foreach (var file in Directory.GetFiles(srcDir))
        {
            var dst = Path.Combine(dstDir, Path.GetFileName(file));
            try { File.Move(file, dst, true); }
            catch { File.Copy(file, dst, true); TryDeleteFile(file); }
        }
    }

    private static void TryDeleteFile(string path)
    {
        // 文件可能被杀毒/索引服务短暂占用：重试几次确保真正删除
        for (int i = 0; i < 3; i++)
        {
            try
            {
                if (!File.Exists(path)) return;
                File.Delete(path);
                return;
            }
            catch
            {
                Thread.Sleep(60);
            }
        }
    }

    private void ScheduleSave()
    {
        _saveTimer ??= new Timer(_ => SaveNow(), null, Timeout.Infinite, Timeout.Infinite);
        _saveTimer.Change(500, Timeout.Infinite);
    }

    public void SaveNow()
    {
        List<ClipEntry> snapshot;
        lock (_lock) snapshot = _entries.ToList();
        try
        {
            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            File.WriteAllText(HistoryFile, json);
        }
        catch { }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(HistoryFile)) return;
            var list = JsonSerializer.Deserialize<List<ClipEntry>>(File.ReadAllText(HistoryFile));
            if (list == null) return;
            lock (_lock) _entries.AddRange(list);
        }
        catch { }
    }

    public void Dispose()
    {
        _saveTimer?.Dispose();
        SaveNow();
    }
}
