using MemoryModule;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace X410Launcher.Tools;

public static class Launcher
{
    private static readonly NativeAssembly? _launcher;

    private delegate bool StartProcessPreloadedDelegate(
        [MarshalAs(UnmanagedType.LPWStr)] string applicationName,
        [MarshalAs(UnmanagedType.LPWStr)] string dllName
    );
    private static readonly StartProcessPreloadedDelegate? StartProcessPreloaded;

    static Launcher()
    {
        using var launcherStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            $"X410Launcher.Native.Launcher.{RuntimeInformation.ProcessArchitecture}.dll"
        );

        if (launcherStream is not null)
        {
            _launcher = NativeAssembly.Load(launcherStream, "Launcher.dll");
            if (_launcher is not null)
            {
                StartProcessPreloaded =
                    _launcher.GetDelegate<StartProcessPreloadedDelegate>("StartProcessPreloadedW");
            }
        }
    }

    public static void Launch(string path)
    {
        if (StartProcessPreloaded is not null)
        {
            if (StartProcessPreloaded(path, Paths.GetHelperDllFile()))
            {
                return;
            }
        }

        Process.Start(path);
    }

    public static void LaunchSettings(string path)
    {
        Process.Start(new ProcessStartInfo()
        {
            FileName = Paths.GetSettingsAppFile(),
            Arguments = $"/hw {(ulong)X410.FindRootWindow():X}"
        });
    }
}
