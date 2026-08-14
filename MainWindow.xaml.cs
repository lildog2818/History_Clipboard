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
    private bool _suppressAutoHide;

    public MainWindow()
    {
        InitializeComponent();
        Services.Store.Changed += OnStoreChanged;
    }

    public void ShowBar()
    {
        Refresh();
        PositionNearCursor();
        SearchBox.Clear();
        Show();
        Activate();
        WindowEffects.ApplyBackdrop(this, ThemeManager.IsDarkEffective(Services.Settings.Current.Theme));
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
    }

    public void HideBar()
    {
        if (IsVisible) Hide();
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
        ShowBar();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => HideBar();

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        SearchBox.Focus();
    }

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

        IEnumerable<ClipEntry> items = Services.Store.Entries;
        if (q.Length > 0)
            items = items.Where(e => Matches(e, q)).ToList();

        List.ItemsSource = items
            .OrderByDescending(e => e.Pinned)
            .ThenByDescending(e => e.CreatedAt)
            .ToList();

        if (List.Items.Count > 0) List.SelectedIndex = 0;
        StatusText.Text = $"{List.Items.Count} 条结果";
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

    private ClipEntry? Selected => List.SelectedItem as ClipEntry;

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
                if (List.Items.Count > 0) { List.SelectedIndex = 0; List.Focus(); }
                break;
            case Key.Escape:
                e.Handled = true;
                HideBar();
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
                if (List.SelectedIndex > 0) List.SelectedIndex--;
                break;
            case Key.Down:
                e.Handled = true;
                if (List.SelectedIndex < List.Items.Count - 1) List.SelectedIndex++;
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

    private void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var entry = Selected;
        if (entry != null) PasteSelected(false);
    }

    private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var entry = Selected;
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
    }

    private void List_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var item = ItemsControl.ContainerFromElement(List, e.OriginalSource as DependencyObject) as ListBoxItem;
        if (item != null) item.IsSelected = true;

        var entry = Selected;
        if (entry == null) return;

        var menu = new ContextMenu();
        menu.Items.Add(NewMenuItem("粘贴", () => PasteSelected(false)));
        menu.Items.Add(NewMenuItem("粘贴为纯文本", () => PasteSelected(true)));
        menu.Items.Add(new Separator());
        menu.Items.Add(NewMenuItem(entry.IsImage ? "复制图片" : "复制文字", CopySelected));
        if (entry.IsImage)
            menu.Items.Add(NewMenuItem("贴图", () => OpenPinnedImage(entry)));
        menu.Items.Add(new Separator());
        menu.Items.Add(NewMenuItem(entry.Pinned ? "取消置顶" : "置顶", TogglePinSelected));
        menu.Items.Add(NewMenuItem("备注/标签", EditNoteSelected));
        menu.Items.Add(new Separator());
        menu.Items.Add(NewMenuItem("删除", DeleteSelected));
        menu.IsOpen = true;
    }

    private static MenuItem NewMenuItem(string header, Action action)
    {
        var mi = new MenuItem { Header = header };
        mi.Click += (_, _) => action();
        return mi;
    }

    private void PasteSelected(bool plain)
    {
        var entry = Selected;
        if (entry == null) return;
        HideBar();
        Services.Paster.Paste(entry, plain);
    }

    private void PasteIndex(int index, bool plain)
    {
        if (List.Items.Count == 0) return;
        if (index < 0 || index >= List.Items.Count) return;
        var entry = List.Items[index] as ClipEntry;
        if (entry == null) return;
        HideBar();
        Services.Paster.Paste(entry, plain);
    }

    private void CopySelected()
    {
        var entry = Selected;
        if (entry == null) return;
        if (entry.IsImage)
        {
            var bmp = Paster.LoadBitmap(entry);
            if (bmp != null) Services.Writer.SetImage(bmp);
        }
        else
        {
            Services.Writer.SetText(entry.PlainText);
        }
        HideBar();
    }

    private void TogglePinSelected()
    {
        var entry = Selected;
        if (entry == null) return;
        Services.Store.TogglePin(entry.Id);
        Refresh();
    }

    private void DeleteSelected()
    {
        var entry = Selected;
        if (entry == null) return;
        Services.Store.Remove(entry.Id);
        Refresh();
    }

    private void EditNoteSelected()
    {
        var entry = Selected;
        if (entry == null) return;
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
