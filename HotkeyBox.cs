using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using ClipboardHistory.Core;

namespace ClipboardHistory;

public sealed class HotkeyBox : TextBox
{
    private HotkeySetting? _value;

    public HotkeySetting? Value
    {
        get => _value;
        set { _value = value; RefreshText(); }
    }

    public HotkeyBox()
    {
        IsReadOnly = true;
        Cursor = Cursors.Hand;
        FontSize = 14;
        PreviewKeyDown += OnKey;
        GotKeyboardFocus += (_, _) => SelectAll();
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (e.Key == Key.Escape || e.Key == Key.Back)
        {
            _value = new HotkeySetting { Modifiers = 0, Key = 0 };
            RefreshText();
            return;
        }
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        uint mods = 0;
        var m = Keyboard.Modifiers;
        if (m.HasFlag(ModifierKeys.Control)) mods |= NativeMethods.MOD_CONTROL;
        if (m.HasFlag(ModifierKeys.Alt)) mods |= NativeMethods.MOD_ALT;
        if (m.HasFlag(ModifierKeys.Shift)) mods |= NativeMethods.MOD_SHIFT;
        if (m.HasFlag(ModifierKeys.Windows)) mods |= NativeMethods.MOD_WIN;

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(e.Key);
        _value = new HotkeySetting { Modifiers = mods, Key = vk };
        RefreshText();
    }

    private void RefreshText() => Text = Format(_value);

    public static string Format(HotkeySetting? h)
    {
        if (h == null || h.Key == 0) return "（未设置）";
        var parts = new List<string>();
        if ((h.Modifiers & NativeMethods.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((h.Modifiers & NativeMethods.MOD_ALT) != 0) parts.Add("Alt");
        if ((h.Modifiers & NativeMethods.MOD_SHIFT) != 0) parts.Add("Shift");
        if ((h.Modifiers & NativeMethods.MOD_WIN) != 0) parts.Add("Win");
        parts.Add(KeyName(h.Key));
        return string.Join(" + ", parts);
    }

    private static string KeyName(uint vk)
    {
        int v = (int)vk;
        if (v >= 0x30 && v <= 0x39) return ((char)('0' + (v - 0x30))).ToString();
        if (v >= 0x41 && v <= 0x5A) return ((char)('A' + (v - 0x41))).ToString();
        if (v >= 0x70 && v <= 0x7B) return "F" + (v - 0x70 + 1);
        var key = KeyInterop.KeyFromVirtualKey(v);
        return key == Key.None ? "VK" + v : key.ToString();
    }
}
