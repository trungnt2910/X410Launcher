using System;
using System.Diagnostics;
using System.IO;

namespace X410Launcher.Tools;

public static class Paths
{
    public static string GetStartupShortcutFile()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "X410Launcher.lnk");
    }

    public static string GetLauncherFile()
    {
        return Process.GetCurrentProcess().MainModule!.FileName!;
    }

    public static string GetAppInstallPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "X410Launcher",
            "appx"
            );
    }

    public static string GetAppFile()
    {
        return Path.Combine(GetAppInstallPath(), "X410", "X410.exe");
    }

    public static string GetHelperDllFile()
    {
        return Path.Combine(GetAppInstallPath(), "X410", "X410.dll");
    }
}
