using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClipboardHistory.Core;

namespace ClipboardHistory;

public partial class SettingsWindow : Window
{
    private readonly List<string> _excluded = new();

    public SettingsWindow()
    {
        InitializeComponent();
        Icon = App.LoadAppIcon();
        LoadValues();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        WindowEffects.ApplyBackdrop(this, ThemeManager.IsDarkEffective(Services.Settings.Current.Theme));
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void LoadValues()
    {
        var s = Services.Settings.Current;

        SearchHotkeyBox.Value = s.SearchHotkey;
        ScreenshotHotkeyBox.Value = s.ScreenshotHotkey;

        ThemeCombo.Items.Add(new ComboBoxItem { Content = "跟随系统", Tag = "system" });
        ThemeCombo.Items.Add(new ComboBoxItem { Content = "浅色", Tag = "light" });
        ThemeCombo.Items.Add(new ComboBoxItem { Content = "深色", Tag = "dark" });
        SelectComboByTag(ThemeCombo, s.Theme);

        MaxEntriesCombo.Items.Add(new ComboBoxItem { Content = "仅手动清理（不自动删）", Tag = 0 });
        MaxEntriesCombo.Items.Add(new ComboBoxItem { Content = "最多 500 条", Tag = 500 });
        MaxEntriesCombo.Items.Add(new ComboBoxItem { Content = "最多 1000 条", Tag = 1000 });
        MaxEntriesCombo.Items.Add(new ComboBoxItem { Content = "最多 5000 条", Tag = 5000 });
        SelectComboByTag(MaxEntriesCombo, s.MaxEntries);

        DataDirBox.Text = s.DataDirectory;
        RestoreClipboardBox.IsChecked = s.RestoreClipboardAfterPaste;
        QuickPasteBox.IsChecked = s.QuickPasteNumberKeys;
        PlainTextOnlyBox.IsChecked = s.PlainTextOnly;
        AutoStartBox.IsChecked = s.AutoStart;

        _excluded.AddRange(s.ExcludedApps ?? Array.Empty<string>());
        ExcludedList.ItemsSource = _excluded;
    }

    private static void SelectComboByTag(ComboBox combo, object tag)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (Equals(item.Tag, tag))
            {
                combo.SelectedItem = item;
                return;
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "选择历史记录保存目录" };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            DataDirBox.Text = dlg.SelectedPath;
    }

    private void AddExcluded_Click(object sender, RoutedEventArgs e)
    {
        var name = ExcludedInput.Text.Trim();
        if (name.Length == 0) return;
        if (!_excluded.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
            _excluded.Add(name);
        ExcludedList.Items.Refresh();
        ExcludedInput.Clear();
    }

    private void ExcludedInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            AddExcluded_Click(sender, e);
        }
    }

    private void RemoveExcluded_Click(object sender, RoutedEventArgs e)
    {
        if (ExcludedList.SelectedItem is string sel)
        {
            _excluded.Remove(sel);
            ExcludedList.Items.Refresh();
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var s = Services.Settings.Current;

        var search = SearchHotkeyBox.Value;
        var shot = ScreenshotHotkeyBox.Value;
        if (search == null || search.Key == 0 || shot == null || shot.Key == 0)
        {
            MessageBox.Show("请先设置两个快捷键。", "设置", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var newDir = DataDirBox.Text.Trim();
        if (newDir.Length == 0)
        {
            MessageBox.Show("保存目录不能为空。", "设置", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!string.Equals(newDir, s.DataDirectory, StringComparison.OrdinalIgnoreCase))
        {
            if (!Services.Store.ChangeDataDirectory(newDir))
            {
                MessageBox.Show("保存目录无效或不可写。", "设置", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        s.SearchHotkey = search;
        s.ScreenshotHotkey = shot;
        s.Theme = (string)((ComboBoxItem)ThemeCombo.SelectedItem).Tag;
        s.MaxEntries = (int)((ComboBoxItem)MaxEntriesCombo.SelectedItem).Tag;
        s.RestoreClipboardAfterPaste = RestoreClipboardBox.IsChecked == true;
        s.QuickPasteNumberKeys = QuickPasteBox.IsChecked == true;
        s.PlainTextOnly = PlainTextOnlyBox.IsChecked == true;
        s.ExcludedApps = _excluded.ToArray();
        s.AutoStart = AutoStartBox.IsChecked == true;

        Services.Settings.Save();
        bool ok = ((App)Application.Current).ApplySettings();
        if (!ok)
            MessageBox.Show("快捷键注册失败，可能被其他程序占用，请更换快捷键。", "设置", MessageBoxButton.OK, MessageBoxImage.Warning);
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
