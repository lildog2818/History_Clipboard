using System;
using System.Windows;
using Microsoft.Win32;

namespace ClipboardHistory.Core;

public static class ThemeManager
{
    public static bool IsDarkEffective(string theme)
    {
        if (theme == "light") return false;
        if (theme == "dark") return true;
        return IsSystemDark();
    }

    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return (key?.GetValue("AppsUseLightTheme") as int? ?? 1) == 0;
        }
        catch { return false; }
    }

    public static void Apply(string theme)
    {
        bool dark = IsDarkEffective(theme);
        var uri = new Uri(dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative);
        var dict = new ResourceDictionary { Source = uri };
        var merged = Application.Current.Resources.MergedDictionaries;
        merged.Clear();
        merged.Add(dict);
    }
}
