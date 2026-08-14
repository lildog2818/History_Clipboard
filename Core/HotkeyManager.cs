using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;

namespace ClipboardHistory.Core;

public sealed class HotkeyManager : IDisposable
{
    private HwndSource? _source;
    private readonly Dictionary<int, Action> _handlers = new();
    private int _nextId = 1;

    public void Attach()
    {
        var parameters = new HwndSourceParameters("ClipboardHistoryHotkeys")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ExtendedWindowStyle = 0
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public bool Register(uint modifiers, uint key, Action handler)
    {
        if (_source == null) return false;
        int id = _nextId++;
        if (NativeMethods.RegisterHotKey(_source.Handle, id, modifiers, key))
        {
            _handlers[id] = handler;
            return true;
        }
        return false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_handlers.TryGetValue(id, out var handler))
            {
                handler();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Clear()
    {
        if (_source == null) return;
        foreach (var id in _handlers.Keys.ToList())
            NativeMethods.UnregisterHotKey(_source.Handle, id);
        _handlers.Clear();
    }

    public void Dispose()
    {
        if (_source != null)
        {
            foreach (var id in _handlers.Keys)
                NativeMethods.UnregisterHotKey(_source.Handle, id);
            _handlers.Clear();
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }
    }
}
