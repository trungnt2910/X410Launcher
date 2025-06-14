using System;
using System.Runtime.InteropServices;

namespace X410Launcher.Tools;

public static class X410
{
    public const uint WM_APP = 32768U;
    public const uint WM_X410INT = 33178U;

    public enum IntMsg : uint
    {
        AreYouThere = 1,
        HaveAnyXClient = 2,
        GetDisplayNumber = 3,
        GetHyperVAddresses = 4,

        DpiScaling = 10,
        SharedClipboard = 11,

        AlwaysBottomDesktop = 25,
        SetFocus = 28,
        Restart = 29,
        Exit = 30,

        SubscriptionStatus = 50
    }

    [Flags]
    public enum IntCmd : uint
    {
        DpiScaling_NONE = 0,
        DpiScaling_DEFAULT = 1,
        DpiScaling_HQ = 2,

        SharedClipboardFlag_ENABLE = 1,
        SharedClipboardFlag_AUTOCOPY = 16,

        OnRestart_SaveDesktopPlacement = 1,

        SubscriptionStatus_GET = 0,
        SubscriptionStatus_UPDATE = 1
    }

    public enum SubscriptionStatus
    {
        Unknown,
        SubscriptionActive,
        SubscriptionExpired,
        TrialValid,
        TrialExpired,

        NoAppUseEntitlement = 10,
        StoreError = 11
    }

    public const string X410_RootWin = "X410_RootWin";

    private static readonly Random _random = new();

    public static bool AreYouThere()
    {
        nint value = _random.Next(1, int.MaxValue);

        return _TrySendX410Message(IntMsg.AreYouThere, value, out var result)
            && result == value;
    }

    public static bool HaveAnyXClient()
    {
        return _TrySendX410Message(IntMsg.HaveAnyXClient, 0u, out var result) && result != 0;
    }

    public static uint? GetDisplayNumber()
    {
        return _TrySendX410Message(IntMsg.GetDisplayNumber, 0u, out var number)
            ? (uint)number : null;
    }

    public static SubscriptionStatus GetSubscriptionStatus()
    {
        if (!_TrySendX410Message(
            IntMsg.SubscriptionStatus, IntCmd.SubscriptionStatus_GET,
            out var response
        ))
        {
            return SubscriptionStatus.Unknown;
        }

        var status = (SubscriptionStatus)(ushort)(response >> 16);
        if (!Enum.IsDefined(typeof(SubscriptionStatus), status))
        {
            return SubscriptionStatus.Unknown;
        }

        return status;
    }

    public static bool SetFocus()
    {
        return _TryPostX410Message(IntMsg.SetFocus, 0u);
    }

    public static bool AppExit()
    {
        return _TryPostX410Message(IntMsg.Exit, 0u);
    }

    public static nint FindRootWindow()
    {
        return FindWindow(X410_RootWin, null);
    }

    private static bool _TrySendX410Message(IntMsg msg, IntCmd cmd, out nint result)
        => _TrySendX410Message(msg, (nint)cmd, out result);

    private static bool _TrySendX410Message(IntMsg msg, nint cmd, out nint result)
    {
        nint hWnd = FindRootWindow();
        if (hWnd == 0)
        {
            result = 0;
            return false;
        }
        result = SendMessage(hWnd, WM_X410INT, (nuint)msg, cmd);
        return true;
    }

    private static bool _TryPostX410Message(IntMsg msg, IntCmd cmd)
        => _TryPostX410Message(msg, (nint)cmd);

    private static bool _TryPostX410Message(IntMsg msg, nint cmd)
    {
        nint hWnd = FindRootWindow();
        if (hWnd == 0)
        {
            return false;
        }
        return PostMessage(hWnd, WM_X410INT, (nuint)msg, cmd);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern nint FindWindow(
        [MarshalAs(UnmanagedType.LPTStr)] string? className,
        [MarshalAs(UnmanagedType.LPTStr)] string? windowName
    );

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool PostMessage(
        nint hWnd,
        uint msg,
        nuint wParam,
        nint lParam
    );

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint SendMessage(
        nint hWnd,
        uint msg,
        nuint wParam,
        nint lParam
    );
}
