using System;
using System.IO;
using System.Text.Json;

namespace ClipboardHistory.Core;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _file;

    public Settings Current { get; private set; }

    public SettingsStore()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipboardHistory");
        _file = Path.Combine(baseDir, "settings.json");
        Current = LoadOrCreate(baseDir);
    }

    public string SettingsFile => _file;

    private Settings LoadOrCreate(string baseDir)
    {
        try
        {
            if (File.Exists(_file))
            {
                var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(_file));
                if (s != null)
                {
                    if (string.IsNullOrWhiteSpace(s.DataDirectory))
                        s.DataDirectory = baseDir;
                    return s;
                }
            }
        }
        catch { /* fall through to default */ }

        var def = new Settings { DataDirectory = baseDir };
        Directory.CreateDirectory(baseDir);
        SaveInternal(def);
        return def;
    }

    public void Save()
    {
        SaveInternal(Current);
    }

    private void SaveInternal(Settings s)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(s, JsonOptions));
        }
        catch { }
    }
}
