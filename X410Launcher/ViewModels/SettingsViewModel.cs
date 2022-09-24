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
    const string ArgumentLaunch = "--launch";
    const string ArgumentUpdate = "--update";
    const string ArgumentNoUi = "--no-ui";

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

    public SettingsViewModel()
    {
        ReadSettingsFromShortcut();
        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
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

            StartupStartX410 = arguments.Contains(ArgumentLaunch);
            StartupUpdateX410 = arguments.Contains(ArgumentUpdate);
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
           
            var arguments = new List<string>() { ArgumentNoUi };
            
            if (StartupStartX410)
            {
                arguments.Add(ArgumentLaunch);
            }

            if (StartupUpdateX410)
            {
                arguments.Add(ArgumentUpdate);
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
