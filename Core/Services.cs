namespace ClipboardHistory.Core;

public static class Services
{
    public static SettingsStore Settings { get; set; } = null!;
    public static ClipboardStore Store { get; set; } = null!;
    public static ClipboardMonitor Monitor { get; set; } = null!;
    public static ClipboardWriter Writer { get; set; } = null!;
    public static Paster Paster { get; set; } = null!;
    public static HotkeyManager Hotkeys { get; set; } = null!;
}
