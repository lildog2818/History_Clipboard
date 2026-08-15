using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClipboardHistory.Core;

namespace ClipboardHistory;

public partial class MainWindow : Window
{
    private enum TabKind { Text, Image, File }

    private TabKind _activeTab = TabKind.Text;
    private bool _suppressAutoHide;

    public MainWindow()
    {
        InitializeComponent();
        Services.Store.Changed += OnStoreChanged;
        ApplySavedSize();
        TextTab.IsChecked = true;
    }

    public void ShowBar(bool restorePosition)
    {
        Refresh();
        ApplySavedSize();
        if (restorePosition) RestorePosition(); else PositionNearCursor();
        Show();
        Activate();
        WindowEffects.ApplyBackdrop(this, ThemeManager.IsDarkEffective(Services.Settings.Current.Theme));
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
    }

    public void HideBar()
    {
        if (!IsVisible) return;
        SaveBounds();
        Hide();
    }

    private void OnStoreChanged() => Dispatcher.Invoke(Refresh);

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        WindowEffects.ApplyBackdrop(this, ThemeManager.IsDarkEffective(Services.Settings.Current.Theme));
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (!_suppressAutoHide) HideBar();
    }

    // ---------- 窗口位置/大小记忆 ----------

    private void ApplySavedSize()
    {
        var s = Services.Settings.Current;
        if (s.WindowW >= 300 && s.WindowH >= 400)
        {
            Width = s.WindowW;
            Height = s.WindowH;
        }
    }

    private void RestorePosition()
    {
        var s = Services.Settings.Current;
        if (s.WindowW >= 300)
        {
            Left = s.WindowX;
            Top = s.WindowY;
            ClampToScreen();
        }
        else
        {
            PositionNearCursor();
        }
    }

    private void SaveBounds()
    {
        var s = Services.Settings.Current;
        s.WindowX = Left;
        s.WindowY = Top;
        s.WindowW = Width;
        s.WindowH = Height;
        Services.Settings.Save();
    }

    private void ClampToScreen()
    {
        double scale = NativeMethods.GetDpiForSystem() / 96.0;
        var vs = System.Windows.Forms.SystemInformation.VirtualScreen;
        double vsLeft = vs.Left / scale, vsTop = vs.Top / scale;
        double vsRight = vsLeft + vs.Width / scale, vsBottom = vsTop + vs.Height / scale;
        if (Left < vsLeft) Left = vsLeft;
        if (Top < vsTop) Top = vsTop;
        if (Left + Width > vsRight) Left = vsRight - Width;
        if (Top + Height > vsBottom) Top = vsBottom - Height;
    }

    // ---------- 标题栏 ----------

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }
        if (e.LeftButton == MouseButtonState.Pressed && WindowState != WindowState.Maximized)
            DragMove();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        MaxBtn.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    private void Max_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        _suppressAutoHide = true;
        try
        {
            var win = new SettingsWindow { Owner = this };
            win.ShowDialog();
        }
        finally
        {
            _suppressAutoHide = false;
        }
        ShowBar(false);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => HideBar();

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        SearchBox.Focus();
    }

    // ---------- 页签 ----------

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (TextTab.IsChecked == true) SwitchTab(TabKind.Text);
        else if (ImageTab.IsChecked == true) SwitchTab(TabKind.Image);
        else if (FileTab.IsChecked == true) SwitchTab(TabKind.File);
    }

    private void SwitchTab(TabKind kind)
    {
        _activeTab = kind;
        TextList.Visibility = kind == TabKind.Text ? Visibility.Visible : Visibility.Collapsed;
        ImageGrid.Visibility = kind == TabKind.Image ? Visibility.Visible : Visibility.Collapsed;
        FileList.Visibility = kind == TabKind.File ? Visibility.Visible : Visibility.Collapsed;
        SelectFirstInActiveTab();
        UpdateStatus();
    }

    private ListBox ActiveList => _activeTab switch
    {
        TabKind.Image => ImageGrid,
        TabKind.File => FileList,
        _ => TextList
    };

    private void SelectFirstInActiveTab()
    {
        var list = ActiveList;
        if (list.Items.Count > 0) list.SelectedIndex = 0;
        else
        {
            PreviewPanel.Visibility = Visibility.Collapsed;
            StatusText.Text = "0 条结果";
        }
    }

    private void UpdateStatus()
    {
        StatusText.Text = $"{ActiveList.Items.Count} 条结果";
    }

    // ---------- 搜索与过滤 ----------

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        Hint.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        ClearBtn.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Collapsed : Visibility.Visible;
        Refresh();
    }

    private void Refresh()
    {
        var q = SearchBox.Text.Trim();
        HighlightConverter.CurrentQuery = q;

        var all = Services.Store.Entries.Where(e => Matches(e, q)).ToList();

        TextList.ItemsSource = all.Where(e => !e.IsImage && !e.IsFileList)
            .OrderByDescending(e => e.Pinned).ThenByDescending(e => e.CreatedAt).ToList();
        ImageGrid.ItemsSource = all.Where(e => e.IsImage)
            .OrderByDescending(e => e.Pinned).ThenByDescending(e => e.CreatedAt).ToList();
        FileList.ItemsSource = all.Where(e => e.IsFileList)
            .OrderByDescending(e => e.Pinned).ThenByDescending(e => e.CreatedAt).ToList();

        SelectFirstInActiveTab();
    }

    private static bool Matches(ClipEntry e, string q)
    {
        return e.PlainText.Contains(q, StringComparison.OrdinalIgnoreCase)
            || (e.Note?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
            || e.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    private void PositionNearCursor()
    {
        NativeMethods.GetCursorPos(out var p);
        double scale = NativeMethods.GetDpiForSystem() / 96.0;
        double left = p.X / scale;
        double top = p.Y / scale;

        var vs = System.Windows.Forms.SystemInformation.VirtualScreen;
        double vsLeft = vs.Left / scale, vsTop = vs.Top / scale;
        double vsW = vs.Width / scale, vsH = vs.Height / scale;

        if (left + Width > vsLeft + vsW) left = vsLeft + vsW - Width;
        if (top + Height > vsTop + vsH) top = vsTop + vsH - Height;
        if (left < vsLeft) left = vsLeft;
        if (top < vsTop) top = vsTop;

        Left = left;
        Top = top;
    }

    // ---------- 键盘 ----------

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool plain = Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                     || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                PasteSelected(plain);
                break;
            case Key.Down:
                e.Handled = true;
                SelectFirstInActiveTab();
                ActiveList.Focus();
                break;
            case Key.Escape:
                e.Handled = true;
                HideBar();
                break;
            case Key.D1:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { e.Handled = true; SwitchTab(TabKind.Text); }
                break;
            case Key.D2:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { e.Handled = true; SwitchTab(TabKind.Image); }
                break;
            case Key.D3:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { e.Handled = true; SwitchTab(TabKind.File); }
                break;
        }
    }

    private void List_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool plain = Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                     || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                PasteSelected(plain);
                break;
            case Key.Escape:
                e.Handled = true;
                HideBar();
                break;
            case Key.Delete:
                e.Handled = true;
                DeleteSelected();
                break;
            case Key.F2:
                e.Handled = true;
                EditNoteSelected();
                break;
            case Key.Up:
                e.Handled = true;
                MoveSelection(-1);
                break;
            case Key.Down:
                e.Handled = true;
                MoveSelection(1);
                break;
            case Key.C:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { e.Handled = true; CopySelected(); }
                break;
            case Key.P:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { e.Handled = true; TogglePinSelected(); }
                break;
            default:
                if (Services.Settings.Current.QuickPasteNumberKeys)
                {
                    int n = -1;
                    if (e.Key >= Key.D1 && e.Key <= Key.D9) n = (int)e.Key - (int)Key.D1 + 1;
                    else if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9) n = (int)e.Key - (int)Key.NumPad1 + 1;
                    if (n > 0)
                    {
                        e.Handled = true;
                        PasteIndex(n - 1, plain);
                    }
                }
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        var list = ActiveList;
        int idx = list.SelectedIndex + delta;
        if (idx < 0) idx = 0;
        if (idx >= list.Items.Count) idx = list.Items.Count - 1;
        list.SelectedIndex = idx;
    }

    private ClipEntry? Selected => ActiveList.SelectedItem as ClipEntry;

    // ---------- 鼠标 ----------

    private void TextList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => PasteSelected(false);
    private void FileList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => PasteSelected(false);
    private void ImageGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var entry = Selected;
        if (entry != null) OpenPinnedImage(entry);
    }

    private void TextList_RightClick(object sender, MouseButtonEventArgs e) => OnListRightClick(TextList, e);
    private void ImageGrid_RightClick(object sender, MouseButtonEventArgs e) => OnListRightClick(ImageGrid, e);
    private void FileList_RightClick(object sender, MouseButtonEventArgs e) => OnListRightClick(FileList, e);

    private void OnListRightClick(ListBox list, MouseButtonEventArgs e)
    {
        var item = ItemsControl.ContainerFromElement(list, e.OriginalSource as DependencyObject) as ListBoxItem;
        if (item == null) return; // 空白处不弹菜单
        item.IsSelected = true;
        var entry = list.SelectedItem as ClipEntry;
        if (entry != null) ShowEntryMenu(entry);
    }

    private void ShowEntryMenu(ClipEntry entry)
    {
        var menu = new ContextMenu();
        menu.Items.Add(NewMenuItem(entry.IsImage ? "复制图片" : "复制文字", () => CopyEntry(entry)));
        if (entry.IsImage)
            menu.Items.Add(NewMenuItem("贴图", () => OpenPinnedImage(entry)));
        menu.Items.Add(new Separator());
        menu.Items.Add(NewMenuItem(entry.Pinned ? "取消置顶" : "置顶", () => TogglePin(entry)));
        menu.Items.Add(NewMenuItem("备注/标签", () => EditNote(entry)));
        menu.Items.Add(new Separator());
        menu.Items.Add(NewMenuItem("删除", () => DeleteEntry(entry)));

        menu.Opened += (_, _) => _suppressAutoHide = true;
        menu.Closed += (_, _) => _suppressAutoHide = false;
        menu.IsOpen = true;
    }

    private static MenuItem NewMenuItem(string header, Action action)
    {
        var mi = new MenuItem { Header = header };
        mi.Click += (_, _) => action();
        return mi;
    }

    // ---------- 预览 ----------

    private void TextList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdatePreview(TextList.SelectedItem as ClipEntry);
    private void ImageGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdatePreview(ImageGrid.SelectedItem as ClipEntry);
    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdatePreview(FileList.SelectedItem as ClipEntry);

    private void UpdatePreview(ClipEntry? entry)
    {
        if (entry == null)
        {
            PreviewPanel.Visibility = Visibility.Collapsed;
            return;
        }

        PreviewPanel.Visibility = Visibility.Visible;
        if (entry.IsImage)
        {
            PreviewText.Visibility = Visibility.Collapsed;
            PreviewFiles.Visibility = Visibility.Collapsed;
            PreviewImage.Visibility = Visibility.Visible;
            PreviewImage.Source = Paster.LoadBitmap(entry);
        }
        else if (entry.IsFileList)
        {
            PreviewText.Visibility = Visibility.Collapsed;
            PreviewImage.Visibility = Visibility.Collapsed;
            PreviewFiles.Visibility = Visibility.Visible;
            PreviewFiles.ItemsSource = entry.Files;
        }
        else
        {
            PreviewImage.Visibility = Visibility.Collapsed;
            PreviewFiles.Visibility = Visibility.Collapsed;
            PreviewText.Visibility = Visibility.Visible;
            PreviewText.Text = entry.PlainText;
        }

        PreviewNote.Text = string.IsNullOrEmpty(entry.Note) ? "" : entry.Note;
        PreviewMeta.Text = $" · {entry.CreatedAt:MM-dd HH:mm} · {entry.SourceApp}";
    }

    // ---------- 操作 ----------

    private void PasteSelected(bool plain)
    {
        var entry = Selected;
        if (entry == null) return;
        HideBar();
        Services.Paster.Paste(entry, plain);
    }

    private void PasteIndex(int index, bool plain)
    {
        var list = ActiveList;
        if (list.Items.Count == 0) return;
        if (index < 0 || index >= list.Items.Count) return;
        var entry = list.Items[index] as ClipEntry;
        if (entry == null) return;
        HideBar();
        Services.Paster.Paste(entry, plain);
    }

    private void CopySelected()
    {
        var entry = Selected;
        if (entry != null) CopyEntry(entry);
    }

    private void CopyEntry(ClipEntry entry)
    {
        if (entry.IsImage)
        {
            var bmp = Paster.LoadBitmap(entry);
            if (bmp != null) Services.Writer.SetImage(bmp);
        }
        else
        {
            Services.Writer.SetText(entry.PlainText);
        }
    }

    private void TogglePinSelected()
    {
        var entry = Selected;
        if (entry != null) TogglePin(entry);
    }

    private void TogglePin(ClipEntry entry)
    {
        Services.Store.TogglePin(entry.Id);
        Refresh();
    }

    private void DeleteSelected()
    {
        var entry = Selected;
        if (entry != null) DeleteEntry(entry);
    }

    private void DeleteEntry(ClipEntry entry)
    {
        Services.Store.Remove(entry.Id);
        Refresh();
    }

    private void EditNoteSelected()
    {
        var entry = Selected;
        if (entry != null) EditNote(entry);
    }

    private void EditNote(ClipEntry entry)
    {
        var res = PromptNote(entry.Note, string.Join(" ", entry.Tags));
        if (res == null) return;
        Services.Store.UpdateMeta(
            entry.Id,
            res.Value.note,
            res.Value.tags.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        Refresh();
    }

    private void OpenPinnedImage(ClipEntry entry)
    {
        if (string.IsNullOrEmpty(entry.ImageFile)) return;
        var win = new PinnedImageWindow(Services.Store.ResolveImage(entry.ImageFile));
        win.Show();
    }

    private (string note, string tags)? PromptNote(string currentNote, string currentTags)
    {
        var win = new Window
        {
            Title = "备注与标签",
            Width = 440,
            Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.SingleBorderWindow
        };
        var sp = new StackPanel { Margin = new Thickness(14) };
        sp.Children.Add(new TextBlock { Text = "备注：", FontSize = 14 });
        var noteBox = new TextBox { Text = currentNote, Margin = new Thickness(0, 4, 0, 10), FontSize = 14 };
        sp.Children.Add(noteBox);
        sp.Children.Add(new TextBlock { Text = "标签（用空格分隔）：", FontSize = 14 });
        var tagBox = new TextBox { Text = currentTags, Margin = new Thickness(0, 4, 0, 12), FontSize = 14 };
        sp.Children.Add(tagBox);
        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "确定", Width = 72, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "取消", Width = 72 };
        btns.Children.Add(ok);
        btns.Children.Add(cancel);
        sp.Children.Add(btns);
        win.Content = sp;

        string note = "", tags = "";
        bool okClicked = false;
        ok.Click += (_, _) => { note = noteBox.Text; tags = tagBox.Text; okClicked = true; win.Close(); };
        cancel.Click += (_, _) => win.Close();

        _suppressAutoHide = true;
        try
        {
            win.ShowDialog();
        }
        finally
        {
            _suppressAutoHide = false;
        }
        Activate();
        SearchBox.Focus();
        return okClicked ? (note, tags) : null;
    }
}
