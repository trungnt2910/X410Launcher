using CommandLine;
using CommandLine.Text;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using X410Launcher.Tools;
using X410Launcher.ViewModels;

namespace X410Launcher;

public class ConsoleApp: IRunnable
{
    private class Options
    {
        [Option('s', Switches.Silent, HelpText = "Prevents spawning and printing text to the console.")]
        public bool IsSilent { get; set; } = false;

        [Option('h', "help", HelpText = "Prints this help and quits immediately.")]
        public bool IsHelp { get; set; } = false;

        [Option("list", HelpText = "Lists versions of X410 available online.")]
        public bool IsList { get; set; } = false;

        [Option("app-version", HelpText = "Outputs the current version of X410 installed.")]
        public bool IsAppVersion { get; set; } = false;

        [Option('i', "install", HelpText = "Installs the latest version of X410.")]
        public bool IsInstall { get; set; } = false;

        [Option("uninstall", HelpText = "Uninstalls X410 from the current machine.")]
        public bool IsUninstall { get; set; } = false;

        [Option('u', Switches.Update, HelpText = "Updates the current installation of X410.")]
        public bool IsUpdate { get; set; } = false;

        [Option('f', "force", HelpText = "Forces the installation or update of X410 even when a copy is already present on the machine.")]
        public bool IsForce { get; set; } = false;

        [Option('l', Switches.Launch, HelpText = "Launches X410.")]
        public bool IsLaunch { get; set; } = false;

        [Option('k', "kill", HelpText = "Kills X410.")]
        public bool IsKill { get; set; } = false;

        [Option(Switches.Tray, HelpText = "Keeps application in tray after command completes.", Hidden = true)]
        public bool IsTray { get; set; } = false;

        [Option(Switches.NoUi, HelpText = "Switches to command line mode.", Hidden = true)]
        public bool IsNoUi { get; set; } = true;

        public bool Validate(out string? error)
        {
            error = null;
            if (new[] { IsInstall, IsUninstall, IsUpdate }.Where(flag => flag).Count() > 1)
            {
                error = "Only one of --install, --uninstall, and --update options may be specified.";
                return false;
            }
            if (new[] { IsLaunch, IsKill }.Where(flag => flag).Count() > 1)
            {
                error = "Only one of --launch and --kill options may be specified.";
                return false;
            }
            return true;
        }
    }

    private readonly Options _options;
    private readonly ParserResult<Options> _parserResult;

    public ConsoleApp(string[] args)
    {
        // Somehow accessing Console APIs (Console.Error, like what Parser.Default does),
        // breaks the streams. We therefore prevent Parser from accessing the console in the early
        // stages and do everything ourselves.
        _parserResult = new Parser(s => { s.HelpWriter = null; s.AutoVersion = false; s.AutoHelp = false; })
            .ParseArguments<Options>(args);
        _options = _parserResult.Value ?? new Options() { IsHelp = true };
    }

    public int Run()
    {
        try
        {
            ConsoleHelpers.SetupConsole(allocate: !_options.IsSilent);
            if (_options.IsSilent)
            {
                Console.SetError(TextWriter.Null);
            }
            return ConsoleMain().Result;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return e.HResult;
        }
        finally
        {
            if (ConsoleHelpers.HasConsole() && Debugger.IsAttached)
            {
                Console.Error.Write("Press any key to continue...");
                Console.ReadKey();
            }
            ConsoleHelpers.CleanupConsole();
        }
    }

    private void DisplayHelp()
    {
        if (ConsoleHelpers.HasConsole())
        {
            var helpText = new HelpText("X410Launcher", string.Empty)
            {
                AutoHelp = false,
                AutoVersion = false,
                AdditionalNewLineAfterOption = false,
                AddDashesToOption = true,
                MaximumDisplayWidth = Console.WindowWidth
            };
            helpText.AddOptions(_parserResult);
            Console.Error.WriteLine(helpText);
        }
    }

    private void DebugInstalledVersion(X410StatusViewModel model)
    {
        if (model.InstalledVersion != null)
        {
            ConsoleHelpers.CleanErrorAndWriteLine($"Version installed on machine: {model.InstalledVersion}");
        }
        else
        {
            ConsoleHelpers.CleanErrorAndWriteLine($"X410 not installed on machine.");
        }
    }

    private async Task<int> ConsoleMain()
    {
        if (_options.IsHelp)
        {
            DisplayHelp();
            return 0;
        }

        if (!_options.Validate(out string? err))
        {
            Console.Error.WriteLine(err);
            return 1;
        }

        var model = new X410StatusViewModel()!;

        if (_options.IsList || _options.IsAppVersion || _options.IsUpdate || _options.IsInstall)
        {
            ConsoleHelpers.CleanErrorAndWriteLine("Collecting data...");
            await model.RefreshAsync();
        }

        if (_options.IsAppVersion)
        {
            DebugInstalledVersion(model);
            if (model.InstalledVersion != null)
            {
                Console.Out.WriteLine(model.InstalledVersion);
            }
            else
            {
                // Should write nothing and fail.
                return 1;
            }
        }

        if (_options.IsList)
        {
            ConsoleHelpers.CleanErrorAndWriteLine($"Found {model.Packages.Count} packages.");
            Console.Out.WriteLine(string.Join(Environment.NewLine, model.Packages.Select(p => p.Version)));
        }

        if (_options.IsUpdate || _options.IsInstall)
        {
            DebugInstalledVersion(model);

            if (_options.IsInstall)
            {
                if (!_options.IsForce && model.InstalledVersion != null)
                {
                    ConsoleHelpers.CleanErrorAndWriteLine($"Version {model.InstalledVersion} already installed.");
                    goto noInstall;
                }
            }

            if (_options.IsUpdate)
            {
                if (model.InstalledVersion != null && !_options.IsForce &&
                    Version.Parse(model.InstalledVersion) >= model.Packages[0].Version)
                {
                    ConsoleHelpers.CleanErrorAndWriteLine($"Latest version {model.InstalledVersion} already installed.");
                    goto noInstall;
                }
            }

            if (_options.IsForce)
            {
                // Pre-emptively kill the server to prevent errors.
                await model.KillAsync(force: true);
            }

            try
            {
                model.PropertyChanged += UpdateConsoleStatus;
                await model.InstallPackageAsync(0);
            }
            finally
            {
                model.PropertyChanged -= UpdateConsoleStatus;
                ConsoleHelpers.CleanLine();
            }
        noInstall:;
        }

        if (_options.IsUninstall)
        {
            await model.UninstallPackageAsync();
        }

        if (_options.IsLaunch)
        {
            await model.LaunchAsync();
        }

        if (_options.IsKill)
        {
            if (!await model.KillAsync(_options.IsForce))
            {
                Console.Error.WriteLine("Failed to kill X410. Close your apps first, or try again with --force.");
            }
        }

        return 0;
    }

    private static void UpdateConsoleStatus(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is X410StatusViewModel model)
        {
            switch (e.PropertyName)
            {
                case nameof(X410StatusViewModel.StatusText):
                {
                    ConsoleHelpers.CleanErrorAndWriteLine(model.StatusText);
                    if (!model.ProgressIsIndeterminate)
                    {
                        ConsoleHelpers.ErrorWriteProgressBar(model.Progress);
                    }
                }
                break;
                case nameof(X410StatusViewModel.Progress):
                {
                    if (!model.ProgressIsIndeterminate)
                    {
                        Console.CursorLeft = 0;
                        ConsoleHelpers.ErrorWriteProgressBar(model.Progress);
                    }
                }
                break;
            }
        }
    }
}
