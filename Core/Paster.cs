using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ClipboardHistory.Core;

public sealed class Paster
{
    private readonly DispatcherTimer _pasteTimer;
    private readonly DispatcherTimer _restoreTimer;
    private ClipEntry? _pending;
    private ClipEntry? _restoreEntry;

    public IntPtr TargetWindow { get; set; }

    public Paster()
    {
        _pasteTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        _pasteTimer.Tick += (_, _) => { _pasteTimer.Stop(); DoPaste(); };
        _restoreTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _restoreTimer.Tick += (_, _) => { _restoreTimer.Stop(); DoRestore(); };
    }

    public void SetRestoreEntry(ClipEntry? entry) => _restoreEntry = entry;

    public void Paste(ClipEntry entry, bool asPlainText)
    {
        Services.Writer.SetData(
            () => BuildDataObject(entry, asPlainText),
            asPlainText ? Hash.OfText(entry.PlainText) : null);
        _pending = entry;
        _pasteTimer.Start();
    }

    private void DoPaste()
    {
        if (_pending == null) return;
        if (TargetWindow != IntPtr.Zero)
        {
            NativeMethods.SetForegroundWindow(TargetWindow);
        }
        NativeMethods.SendCtrlV();
        _pending = null;
        if (Services.Settings.Current.RestoreClipboardAfterPaste && _restoreEntry != null)
            _restoreTimer.Start();
    }

    private void DoRestore()
    {
        if (_restoreEntry == null) return;
        Services.Writer.SetData(() => BuildDataObject(_restoreEntry, false), null);
        _restoreEntry = null;
    }

    public static IDataObject BuildDataObject(ClipEntry entry, bool asPlainText)
    {
        var d = new DataObject();
        if (asPlainText)
        {
            d.SetData(DataFormats.UnicodeText, entry.PlainText ?? "");
            return d;
        }

        if (entry.IsFileList)
        {
            d.SetData(DataFormats.FileDrop, entry.Files);
            return d;
        }

        if (entry.IsImage)
        {
            var bmp = LoadBitmap(entry);
            if (bmp != null) d.SetData(DataFormats.Bitmap, bmp);
            return d;
        }

        d.SetData(DataFormats.UnicodeText, entry.PlainText ?? "");
        if (!string.IsNullOrEmpty(entry.Html)) d.SetData(DataFormats.Html, entry.Html);
        if (!string.IsNullOrEmpty(entry.Rtf)) d.SetData(DataFormats.Rtf, entry.Rtf);
        return d;
    }

    public static BitmapSource? LoadBitmap(ClipEntry entry)
    {
        if (string.IsNullOrEmpty(entry.ImageFile)) return null;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(Services.Store.ResolveImage(entry.ImageFile), UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }
}
