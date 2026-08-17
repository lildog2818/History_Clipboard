using System;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ClipboardHistory.Core;

// 剪贴板写入统一在后台 STA 线程执行：
//   1) 剪贴板被其他程序占用时（CLIPBRD_E_CANT_OPEN），WPF 的 SetDataObject 内部会带 Sleep
//      重试，若在 UI 线程执行会造成界面卡顿数秒——挪到后台线程后 UI 不再阻塞；
//   2) 仍使用 copy=false（延迟渲染），后台线程常驻并泵消息，保证其他程序粘贴取数时能完成渲染；
//   3) 调用方通过 onWritten/onError 回调（在 UI 线程）得知写入结果。
public sealed class ClipboardWriter : IDisposable
{
    private readonly ClipboardMonitor _monitor;
    private readonly Thread _thread;
    private readonly Dispatcher _dispatcher;

    public ClipboardWriter(ClipboardMonitor monitor)
    {
        _monitor = monitor;
        var ready = new ManualResetEventSlim(false);
        Dispatcher? dispatcher = null;
        _thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "ClipboardWriter"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        ready.Wait();
        _dispatcher = dispatcher!;
    }

    // 一律使用 copy=false（延迟渲染）：copy=true 会调用 Flush→OpenClipboard，
    // 在本机/某些环境下会间歇性失败(CLIPBRD_E_CANT_OPEN)。带重试兜底（在后台线程）。
    public void SetData(Func<IDataObject> build,
        string? contentHash,
        Action? onWritten = null,
        Action<Exception>? onError = null)
    {
        _monitor.BeginSelfWrite(contentHash);
        RunOnWorker(build, onWritten, onError);
    }

    public void SetData(IDataObject data,
        Action? onWritten = null,
        Action<Exception>? onError = null)
        => SetData(() => data, TextHashOf(data), onWritten, onError);

    public void SetText(string text,
        Action? onWritten = null,
        Action<Exception>? onError = null)
        => SetData(() =>
        {
            var d = new DataObject();
            d.SetData(DataFormats.UnicodeText, text);
            return d;
        }, Hash.OfText(text), onWritten, onError);

    public void SetImage(BitmapSource image,
        Action? onWritten = null,
        Action<Exception>? onError = null)
        => SetData(() =>
        {
            var d = new DataObject();
            d.SetData(DataFormats.Bitmap, image);
            return d;
        }, null, onWritten, onError);

    private void RunOnWorker(Func<IDataObject> build, Action? onWritten, Action<Exception>? onError)
    {
        var ui = Application.Current?.Dispatcher;
        _dispatcher.BeginInvoke(new Action(() =>
        {
            Exception? error = null;
            try
            {
                var data = build();
                SetWithRetry(() => Clipboard.SetDataObject(data, false));
            }
            catch (Exception ex)
            {
                error = ex;
                Logger.Error("剪贴板写入失败", ex);
            }

            Action finish = () =>
            {
                if (error != null)
                {
                    _monitor.CancelSelfWrite();
                    onError?.Invoke(error);
                }
                else
                {
                    onWritten?.Invoke();
                }
            };
            if (ui != null && !ui.HasShutdownStarted && !ui.HasShutdownFinished)
                ui.BeginInvoke(finish);
            else
                finish();
        }));
    }

    private static void SetWithRetry(Action action)
    {
        Exception? last = null;
        for (int i = 0; i < 4; i++)
        {
            try { action(); return; }
            catch (Exception ex)
            {
                last = ex;
                Thread.Sleep(120);
            }
        }
        if (last != null) throw last;
    }

    private static string? TextHashOf(IDataObject data)
    {
        try
        {
            if (data.GetDataPresent(DataFormats.UnicodeText))
                return Hash.OfText(data.GetData(DataFormats.UnicodeText) as string ?? "");
        }
        catch { }
        return null;
    }

    public void Dispose()
    {
        try
        {
            if (_dispatcher != null && !_dispatcher.HasShutdownStarted)
                _dispatcher.InvokeShutdown();
            _thread.Join(1000);
        }
        catch { }
    }
}
