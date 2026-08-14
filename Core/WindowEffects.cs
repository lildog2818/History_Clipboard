using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ClipboardHistory.Core;

public static class WindowEffects
{
    public static void ApplyBackdrop(Window window, bool dark)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        int build = Environment.OSVersion.Version.Build;

        try
        {
            int darkValue = dark ? 1 : 0;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkValue, 4);
        }
        catch { }

        if (build >= 22621) // Windows 11 22H2+
        {
            try
            {
                int backdrop = NativeMethods.DWMSBT_MAINWINDOW; // Mica
                NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, 4);
                int corner = NativeMethods.DWMWCP_ROUND;
                NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, 4);
                return;
            }
            catch { }
        }
        else if (build >= 17763) // Windows 10 1809+
        {
            try
            {
                ApplyWin10Acrylic(hwnd);
                return;
            }
            catch { }
        }
    }

    private static void ApplyWin10Acrylic(IntPtr hwnd)
    {
        var accent = new NativeMethods.AccentPolicy
        {
            AccentState = NativeMethods.ACCENT_ENABLE_ACRYLICBLURBEHIND,
            AccentFlags = 2,
            GradientColor = unchecked((int)0xCC000000) // ABGR, semi-transparent black
        };
        int accentSize = Marshal.SizeOf(accent);
        IntPtr accentPtr = Marshal.AllocHGlobal(accentSize);
        try
        {
            Marshal.StructureToPtr(accent, accentPtr, false);
            var data = new NativeMethods.WindowCompositionAttributeData
            {
                Attribute = NativeMethods.WCA_ACCENT_POLICY,
                SizeOfData = accentSize,
                Data = accentPtr
            };
            NativeMethods.SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(accentPtr);
        }
    }
}
