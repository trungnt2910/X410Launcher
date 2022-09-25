using System.Threading;
using System;
using System.Windows;

namespace X410Launcher;

internal static class SingleInstanceHelpers
{
    private static bool _alreadyProcessedOnThisInstance;
    private static EventWaitHandle? _eventWaitHandle;
    private static bool _isSecondaryInstance = true;

    public static void Register(string appName, Application? app = null, bool uniquePerUser = true)
    {
        if (_alreadyProcessedOnThisInstance)
        {
            return;
        }
        _alreadyProcessedOnThisInstance = true;

        app ??= Application.Current;

        string eventName = uniquePerUser
            ? $"{appName}-{Environment.MachineName}-{Environment.UserDomainName}-{Environment.UserName}"
            : $"{appName}-{Environment.MachineName}";

        try
        {
            _eventWaitHandle = EventWaitHandle.OpenExisting(eventName);
        }
        catch
        {
            // This code only runs on the first instance.
            _isSecondaryInstance = false;
            RegisterFirstInstanceWindowActivation(app, eventName);
        }
    }

    public static bool IsSecondaryInstance
    {
        get
        {
            RegisterGuard();
            return _isSecondaryInstance;
        }
    }

    public static void ActivateFirstInstanceWindow()
    {
        RegisterGuard();
        // Let's notify the first instance to activate its main window.
        _ = _eventWaitHandle?.Set();
    }

    private static void RegisterGuard()
    {
        if (!_alreadyProcessedOnThisInstance)
        {
            throw new InvalidOperationException($"Must call {nameof(Register)} to use any other {nameof(SingleInstanceHelpers)} API.");
        }
    }

    private static void RegisterFirstInstanceWindowActivation(Application app, string eventName)
    {
        var eventWaitHandle = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            eventName);

        _ = ThreadPool.RegisterWaitForSingleObject(eventWaitHandle, WaitOrTimerCallback, app, Timeout.Infinite, false);

        eventWaitHandle.Close();
    }

    private static void WaitOrTimerCallback(object? state, bool timedOut)
    {
        _ = (state as Application)?.Dispatcher.BeginInvoke(new Action(() =>
        {
            _ = Application.Current.MainWindow.Activate();
        }));
    }
}