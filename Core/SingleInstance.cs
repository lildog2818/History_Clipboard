using System;
using System.Threading;

namespace ClipboardHistory.Core;

public static class SingleInstance
{
    private const string MutexName = "ClipboardHistory_SingleInstance_929A4E21";
    private const string EventName = "ClipboardHistory_ShowEvent_929A4E21";

    private static Mutex? _mutex;
    private static EventWaitHandle? _showEvent;

    public static bool TryAcquire()
    {
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        return createdNew;
    }

    public static void SignalExisting()
    {
        try
        {
            using var evt = EventWaitHandle.OpenExisting(EventName);
            evt.Set();
        }
        catch { }
    }

    public static EventWaitHandle? ShowEvent => _showEvent;

    public static void Release()
    {
        _mutex?.Dispose();
        _showEvent?.Dispose();
        _mutex = null;
        _showEvent = null;
    }
}
