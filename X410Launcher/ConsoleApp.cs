using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System;
using X410Launcher.ViewModels;
using CommandLine;
using System.Threading;
using CommandLine.Text;
using System.Diagnostics;

namespace X410Launcher;

public class ConsoleApp
{
    private class Options
    {
        [Option('s', "silent", HelpText = "Prevents spawning and printing text to the console.")]
        public bool IsSilent { get; set; } = false;

        [Option('h', "help", HelpText = "Prints this help and quits immediately.")]
        public bool IsHelp { get; set; } = false;

        [Option('i', "install", HelpText = "Installs the latest version of X410.")]
        public bool IsInstall { get; set; } = false;

        [Option('u', "uninstall", HelpText = "Uninstalls X410 from the current machine.")]
        public bool IsUninstall { get; set; } = false;

        [Option('u', "update", HelpText = "Updates the current installation of X410.")]
        public bool IsUpdate { get; set; } = false;

        [Option('f', "force", HelpText = "Forces the installation or update of X410 even when a copy is already present on the machine.")]
        public bool IsForce { get; set; } = false;

        [Option('l', "launch", HelpText = "Launches X410.")]
        public bool IsLaunch { get; set; } = false;

        [Option('k', "kill", HelpText = "Kills X410.")]
        public bool IsKill { get; set; } = false;

        [Option("no-ui", HelpText = "Switches to command line mode", Hidden = true)]
        public bool IsNoUi { get; set; } = true;

        public bool Validate(out string? error)
        {
            error = null;
            if (new[] {IsInstall, IsUninstall, IsUpdate}.Where(flag => flag).Count() > 1)
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
        _parserResult = Parser.Default.ParseArguments<Options>(args)
          .WithNotParsed(errors => throw new ArgumentException(string.Join(", ", errors.Select(e => e.Tag.ToString()))));
        _options = _parserResult.Value ?? new Options() { IsHelp = true };
    }

    public void Run()
    {
        try
        {
            ConsoleHelpers.SetupConsole(allocate: !_options.IsSilent);
            if (!_options.IsSilent)
            {
                Console.SetError(Console.Out);
            }
            ConsoleMain().Wait();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            throw;
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

    private async Task ConsoleMain()
    {
        if (_options.IsHelp)
        {
            DisplayHelp();
            return;
        }

        if (!_options.Validate(out string? err))
        {
            Console.Error.WriteLine(err);
            Environment.ExitCode = 1;
            return;
        }

        var model = new X410StatusViewModel()!;

        if (_options.IsUpdate || _options.IsInstall)
        {
            await model.RefreshAsync();
            ConsoleHelpers.CleanErrorAndWriteLine($"Version installed on machine: {model.InstalledVersion}");

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
                if (model.InstalledVersion != null &&
                    Version.Parse(model.InstalledVersion) >= model.Packages[0].Version)
                {
                    ConsoleHelpers.CleanErrorAndWriteLine($"Latest version {model.InstalledVersion} already installed.");
                    goto noInstall;
                }
            }

            model.PropertyChanged += UpdateConsoleStatus;
            await model.InstallPackageAsync(0);
            model.PropertyChanged -= UpdateConsoleStatus;
            ConsoleHelpers.CleanLine();
        noInstall:;
        }

        if (_options.IsUninstall)
        {
            await model.UninstallPackageAsync();
        }

        if (_options.IsLaunch)
        {
            model.Launch();
        }

        if (_options.IsKill)
        {
            await model.KillAsync();
        }
    }

    private static void UpdateConsoleStatus(object sender, PropertyChangedEventArgs e)
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
