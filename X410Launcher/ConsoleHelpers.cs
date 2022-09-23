using System;
using System.Runtime.InteropServices;
using System.Text;

namespace X410Launcher;

internal static class ConsoleHelpers
{
    private static bool _consoleAttached = false;
    private static bool _consoleAllocated = false;
    private static string _promptString = string.Empty;

    public static bool SetupConsole(bool allocate = true)
    {
        if (HasConsole())
        {
            return true;
        }

        _consoleAttached = AttachConsole();

        if (_consoleAttached)
        {
            // We assume that the prompt string is only one line.
            if (Console.CursorLeft != 0)
            {
                _promptString = ReadLineAt(0, Console.CursorTop, Console.CursorLeft);
                CleanLine();
                --Console.CursorTop;
            }
        }
        else
        {
            if (allocate)
            {
                _consoleAllocated = AllocConsole();
            }
        }

        return HasConsole();
    }

    public static bool HasConsole()
    {
        return _consoleAttached || _consoleAllocated;
    }

    public static void CleanupConsole()
    {
        if (_consoleAttached)
        {
            if (Console.CursorLeft != 0)
            {
                Console.WriteLine();
            }
            Console.Write(_promptString);
        }
    }

    public static void CleanErrorAndWriteLine(string format, params object[] objects)
    {
        if (HasConsole())
        {
            CleanLine();
            Console.Error.WriteLine(format, objects);
        }
    }

    public static void ErrorWriteProgressBar(double progress)
    {
        if (HasConsole())
        {
            CleanLine();
            var totalWidth = Console.WindowWidth - 1;
            var numberIndicatorWidth = 5; // 100%, 5 characters (including space)
            var bracketsWidth = 2; // [ and ]
            var progressWidth = totalWidth - numberIndicatorWidth - bracketsWidth;
            var fillerWidth = (int)((decimal)progress / 100 * progressWidth);
            var whiteWidth = progressWidth - fillerWidth;

            Console.Error.Write($"[{new string('=', fillerWidth)}{new string(' ', whiteWidth)}] {(int)progress}%");
        }
    }

    public static void CleanLine()
    {
        if (HasConsole())
        {
            var oldTop = Console.CursorTop;
            Console.CursorLeft = 0;
            Console.Write(new string(' ', Console.WindowWidth));
            Console.CursorLeft = 0;
            Console.CursorTop = oldTop;
        }
    }

    private static string ReadLineAt(int x, int y, int length)
    {
        IntPtr consoleHandle = GetStdHandle(-11);
        if (consoleHandle == IntPtr.Zero)
        {
            return string.Empty;
        }
        var position = new COORD
        {
            X = (short)x,
            Y = (short)y
        };
        var result = new StringBuilder(length);
        if (ReadConsoleOutputCharacter(consoleHandle, result, (uint)length, position, out var _))
        {
            var resultString = result.ToString();
            return resultString.Substring(0, Math.Min(resultString.Length, length));
        }
        else
        {
            return string.Empty;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int pid = -1);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadConsoleOutputCharacter(
        IntPtr hConsoleOutput,
        [Out] StringBuilder lpCharacter,
        uint length,
        COORD bufferCoord,
        out uint lpNumberOfCharactersRead);

    [StructLayout(LayoutKind.Sequential)]
    private struct COORD
    {
        public short X;
        public short Y;
    }
}
