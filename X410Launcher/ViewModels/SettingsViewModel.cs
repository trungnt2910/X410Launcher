using CommandLine;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellLink;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using X410Launcher.Tools;

namespace X410Launcher.ViewModels;

public class SettingsViewModel : ObservableObject
{
    private bool _startupStartX410;
    public bool StartupStartX410
    {
        get => _startupStartX410;
        set => SetProperty(ref _startupStartX410, value);
    }

    private bool _startupUpdateX410;
    public bool StartupUpdateX410
    {
        get => _startupUpdateX410;
        set => SetProperty(ref _startupUpdateX410, value);
    }

    private bool _startupAddIconToTray;
    public bool StartupAddIconToTray
    {
        get => _startupAddIconToTray;
        set => SetProperty(ref _startupAddIconToTray, value);
    }

    public SettingsViewModel()
    {
        ReadSettingsFromShortcut();
        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        WriteSettingsToShortcut();
    }

    private void ReadSettingsFromShortcut()
    {
        PropertyChanged -= OnPropertyChanged;
        var path = Paths.GetStartupShortcutFile();
        if (File.Exists(path))
        {
            var shortcut = Shortcut.ReadFromFile(path);
            var arguments = (shortcut.StringData.CommandLineArguments ?? string.Empty).SplitArgs().ToHashSet();

            StartupStartX410 = arguments.Contains(Switches.LaunchSwitch);
            StartupUpdateX410 = arguments.Contains(Switches.UpdateSwitch);
            StartupAddIconToTray = arguments.Contains(Switches.TraySwitch);
        }
        else
        {
            StartupStartX410 = false;
            StartupUpdateX410 = false;
        }
        PropertyChanged += OnPropertyChanged;
    }

    private void WriteSettingsToShortcut()
    {
        var path = Paths.GetStartupShortcutFile();
        if (StartupStartX410 || StartupUpdateX410)
        {
            var launcherFile = Paths.GetLauncherFile();
           
            var arguments = new List<string>() { Switches.NoUiSwitch, Switches.SilentSwitch };
            
            if (StartupStartX410)
            {
                arguments.Add(Switches.LaunchSwitch);
            }

            if (StartupUpdateX410)
            {
                arguments.Add(Switches.UpdateSwitch);
            }

            if (StartupAddIconToTray)
            {
                arguments.Add(Switches.TraySwitch);
            }

            var shortcut = Shortcut.CreateShortcut(launcherFile, string.Join(" ", arguments), Paths.GetAppFile(), 0);
            shortcut.StringData.NameString = "X410Launcher";
            shortcut.WriteToFile(path);
        }
        else
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
