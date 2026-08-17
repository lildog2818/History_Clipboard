using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using ClipboardHistory.Core;

namespace ClipboardHistory;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private ScreenshotOverlay? _overlay;
    private NotifyIcon? _tray;
    private volatile bool _shutdown;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!SingleInstance.TryAcquire())
        {
            SingleInstance.SignalExisting();
            Shutdown();
            return;
        }

        try
        {
            Services.Settings = new SettingsStore();
            Services.Store = new ClipboardStore(Services.Settings);
            Services.Monitor = new ClipboardMonitor(Services.Store);
            Services.Writer = new ClipboardWriter(Services.Monitor);
            Services.Paster = new Paster();
            Services.Hotkeys = new HotkeyManager();

            Services.Monitor.Attach();
            Services.Hotkeys.Attach();

            ThemeManager.Apply(Services.Settings.Current.Theme);

            _mainWindow = new MainWindow();
            _overlay = new ScreenshotOverlay();

            bool hotkeysOk = RegisterHotkeys();
            InitTray();
            StartSecondInstanceListener();

            if (!hotkeysOk)
                _tray?.ShowBalloonTip(4000, "剪贴板历史",
                    "快捷键注册失败，可能被其他程序占用。请在「设置 → 快捷键」中更换。", ToolTipIcon.Warning);

            if (Services.Settings.Current.FirstRun)
            {
                Services.Settings.Current.FirstRun = false;
                Services.Settings.Save();
                Dispatcher.BeginInvoke(new Action(ShowSearchBar));
                _tray?.ShowBalloonTip(3000, "剪贴板历史",
                    "已启动。Ctrl+` 唤起搜索条，Ctrl+Alt+A 截图。", ToolTipIcon.Info);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("启动失败", ex);
            System.Windows.Forms.MessageBox.Show(
                "启动失败: " + ex.Message, "剪贴板历史",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            Shutdown();
        }
    }

    private bool RegisterHotkeys()
    {
        Services.Hotkeys.Clear();
        var s = Services.Settings.Current;
        bool ok1 = Services.Hotkeys.Register(s.SearchHotkey.Modifiers, s.SearchHotkey.Key, ShowSearchBar);
        bool ok2 = Services.Hotkeys.Register(s.ScreenshotHotkey.Modifiers, s.ScreenshotHotkey.Key, ShowScreenshot);
        if (!ok1 || !ok2) Logger.Error("快捷键注册失败（可能被其他程序占用）");
        return ok1 && ok2;
    }

    public bool ApplySettings()
    {
        bool ok = RegisterHotkeys();
        ThemeManager.Apply(Services.Settings.Current.Theme);
        Autostart.Set(Services.Settings.Current.AutoStart);
        return ok;
    }

    public void ShowSearchBar() => ShowSearchBarCore(false, true);       // 快捷键：不抢焦点，双击粘贴
    public void ShowSearchBarRestored() => ShowSearchBarCore(true, false); // 托盘：双击复制

    private void ShowSearchBarCore(bool restorePosition, bool pasteMode)
    {
        if (_mainWindow == null) return;
        if (_mainWindow.IsVisible)
        {
            _mainWindow.HideBar();
            return;
        }
        _mainWindow.ShowBar(restorePosition, pasteMode);
    }

    public void ShowScreenshot()
    {
        _mainWindow?.HideBar();
        _overlay?.CaptureAndShow();
    }

    private void ShowSettings()
    {
        _mainWindow?.HideBar();
        var win = new SettingsWindow();
        win.ShowDialog();
    }

    private void InitTray()
    {
        _tray = new NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Text = "剪贴板历史",
            Visible = true
        };

        var menu = new ContextMenuStrip();

        var open = new ToolStripMenuItem("打开搜索");
        open.Click += (_, _) => ShowSearchBar();
        menu.Items.Add(open);

        var shot = new ToolStripMenuItem("截图");
        shot.Click += (_, _) => ShowScreenshot();
        menu.Items.Add(shot);

        var settings = new ToolStripMenuItem("设置...");
        settings.Click += (_, _) => ShowSettings();
        menu.Items.Add(settings);

        menu.Items.Add(new ToolStripSeparator());

        var themeSub = new ToolStripMenuItem("主题");
        foreach (var (label, value) in new[] { ("跟随系统", "system"), ("浅色", "light"), ("深色", "dark") })
        {
            var item = new ToolStripMenuItem(label) { Checked = Services.Settings.Current.Theme == value };
            item.Click += (_, _) =>
            {
                Services.Settings.Current.Theme = value;
                Services.Settings.Save();
                ThemeManager.Apply(value);
            };
            themeSub.DropDownItems.Add(item);
        }
        menu.Items.Add(themeSub);

        var dirItem = new ToolStripMenuItem("选择保存目录...");
        dirItem.Click += (_, _) => ChooseDataDirectory();
        menu.Items.Add(dirItem);

        var clear = new ToolStripMenuItem("清空历史");
        clear.Click += (_, _) =>
        {
            var res = System.Windows.Forms.MessageBox.Show(
                "是否保留置顶条目？\n是 = 保留置顶，否 = 全部清空", "清空历史",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (res == DialogResult.Yes) Services.Store.Clear(true);
            else if (res == DialogResult.No) Services.Store.Clear(false);
        };
        menu.Items.Add(clear);

        menu.Items.Add(new ToolStripSeparator());

        var auto = new ToolStripMenuItem("开机自启") { Checked = Autostart.IsEnabled() };
        auto.Click += (_, _) =>
        {
            bool enable = !Autostart.IsEnabled();
            Autostart.Set(enable);
            auto.Checked = enable;
            Services.Settings.Current.AutoStart = enable;
            Services.Settings.Save();
        };
        menu.Items.Add(auto);

        var exit = new ToolStripMenuItem("退出");
        exit.Click += (_, _) => { _shutdown = true; Shutdown(); };
        menu.Items.Add(exit);

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowSearchBarRestored();
    }

    private void ChooseDataDirectory()
    {
        using var dlg = new FolderBrowserDialog { Description = "选择历史记录保存目录" };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            bool ok = Services.Store.ChangeDataDirectory(dlg.SelectedPath);
            System.Windows.Forms.MessageBox.Show(
                ok ? "保存目录已更改。" : "更改失败：目录无效或不可写。",
                "剪贴板历史", MessageBoxButtons.OK,
                ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
    }

    private void StartSecondInstanceListener()
    {
        var evt = SingleInstance.ShowEvent;
        if (evt == null) return;
        var thread = new Thread(() =>
        {
            while (!_shutdown)
            {
                try
                {
                    if (evt.WaitOne(1000))
                        Dispatcher.Invoke(() => ShowSearchBar());
                }
                catch { break; }
            }
        }) { IsBackground = true };
        thread.Start();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _shutdown = true;
        try { Services.Settings?.SaveNow(); } catch { }
        try { Services.Store?.Dispose(); } catch { }
        try { Services.Hotkeys?.Dispose(); } catch { }
        try { Services.Monitor?.Dispose(); } catch { }
        try { _tray?.Dispose(); } catch { }
        SingleInstance.Release();
        base.OnExit(e);
    }

    private static System.Drawing.Icon CreateTrayIcon()
    {
        using var bmp = new System.Drawing.Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);

            var rect = new System.Drawing.Rectangle(3, 1, 26, 30);
            int r = 6;
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(rect.X, rect.Y, r, r, 180, 90);
            path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
            path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
            path.CloseFigure();
            using var body = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(unchecked((int)0xFF4F6EF7)));
            g.FillPath(body, path);

            using var white = new System.Drawing.SolidBrush(System.Drawing.Color.White);
            g.FillRectangle(white, 8, 7, 16, 4);
            g.FillRectangle(white, 8, 14, 16, 4);
            g.FillRectangle(white, 8, 21, 11, 4);
        }
        IntPtr h = bmp.GetHicon();
        try
        {
            using var tmp = System.Drawing.Icon.FromHandle(h);
            return (System.Drawing.Icon)tmp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(h);
        }
    }
}
