using System;
using System.IO;

namespace ClipboardHistory.Core;

public static class Logger
{
    private static readonly object Lock = new();

    public static void Error(string message, Exception? ex = null)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClipboardHistory");
            Directory.CreateDirectory(dir);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} ERROR {message}";
            if (ex != null) line += " | " + ex;
            lock (Lock) File.AppendAllText(Path.Combine(dir, "app.log"), line + Environment.NewLine);
        }
        catch { }
    }
}
