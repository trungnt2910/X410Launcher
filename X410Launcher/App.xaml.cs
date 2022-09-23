using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using X410Launcher.ViewModels;

namespace X410Launcher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (args.Contains("--no-ui"))
            {
                var thread = new Thread((o) => ConsoleMain((string[])o).Wait());
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start(args);
                thread.Join();
            }
            else
            {
                AppMain(args);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AttachConsole(int input);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllocConsole();

        private static async Task ConsoleMain(string[] args)
        {
            bool isUpdate = args.Contains("--update");
            bool isInstall = args.Contains("--install");
            bool isUninstall = args.Contains("--uninstall");
            bool isSilent = args.Contains("--silent");
            bool isForce = args.Contains("--force");
            bool isLaunch = args.Contains("--launch");
            bool isKill = args.Contains("--kill");
            bool isHelp = args.Contains("--help");

            if (isSilent && !isHelp)
            {
                Console.SetError(TextWriter.Null);
            }
            else
            {
                if (AttachConsole(-1))
                {
                    Console.WriteLine();
                }
                else
                {
                    AllocConsole();
                }
            }

            if (isHelp)
            {
                Console.WriteLine(@"X410 Launcher - Console mode.
Available options:
    --help:         Prints this help and quits immediately.
    --silent:       Prevents printing anything to stderr.
    --install:      Installs the latest version of X410.
    --force:        Forces the installation of X410 even when a copy is already present on the machine.
    --update:       Updates the current installation of X410.
    --uninstall:    Uninstalls X410 from the current machine.
    --launch:       Launches X410.
    --kill:         Kills X410.");
                return;
            }

            var model = new X410StatusViewModel();

            if (new[] {isUpdate, isInstall, isUninstall}.Where(flag => flag).Count() > 1)
            {
                Console.Error.WriteLine("Only one of the options --update, --install, and --uninstall can be specified!");
                Environment.Exit(1);
            }

            if (isLaunch && isKill)
            {
                Console.Error.WriteLine("Cannot --launch and --kill at the same time!");
                Environment.Exit(1);
            }

            if (isUpdate || isInstall)
            {
                Console.Error.WriteLine("Searching for packages...");
                await model.RefreshAsync();
                Console.Error.WriteLine($"{model.Packages.Count} packages found.");

                Console.Error.WriteLine($"Installed version: {model.InstalledVersion}");

                if (isInstall)
                {
                    if (!isForce && model.InstalledVersion != null)
                    {
                        Console.Error.WriteLine($"Version {model.InstalledVersion} already installed.");
                        goto noInstall;
                    }
                }

                if (isUpdate)
                {
                    if (Version.Parse(model.InstalledVersion) >= model.Packages[0].Version)
                    {
                        Console.Error.WriteLine("Latest version already installed.");
                        goto noInstall;
                    }
                }

                Console.Error.WriteLine($"Installing: {model.Packages[0].Name}");
                await model.InstallPackageAsync(0);
                Console.Error.WriteLine($"Installed: {model.Packages[0].Name}");
            noInstall:;
            }

            if (isUninstall)
            {
                await model.UninstallPackageAsync();
            }

            if (isLaunch)
            {
                model.Launch();
            }

            if (isKill)
            {
                await model.KillAsync();
            }
        }

        private static void AppMain(string[] args)
        {
            var application = new App();
            application.InitializeComponent();
            application.Run();
        }
    }
}
