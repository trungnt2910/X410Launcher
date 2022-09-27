using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;
using X410Launcher.Tools;
using X410Launcher.ViewModels;
using WinIcon = System.Drawing.Icon;

namespace X410Launcher;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : UiWindow
{
    private readonly X410StatusViewModel _model;

    public MainWindow()
    {
        InitializeComponent();

        // Loaded from StaticResource
        _model = (X410StatusViewModel)DataContext;
        _model.PropertyChanged += UpdateIcon;

        // WPFUI theme watchers.
        Loaded += MainWindow_Loaded;

        // Custom actions to unhide this window from the taskbar. 
        Activated += MainWindow_Activated;

        if (Environment.GetCommandLineArgs().Contains(Switches.TraySwitch))
        {
            MinimizeToTrayButton_Click(null, null);
        }
    }

    private static WinIcon? GetIcon(string fileName)
    {
        return WinIcon.ExtractAssociatedIcon(fileName);
    }

    private static ImageSource? GetIconImage(string fileName)
    {
        using var icon = GetIcon(fileName);
        if (icon != null)
        {
            return Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        new Int32Rect(0, 0, icon.Width, icon.Height),
                        BitmapSizeOptions.FromEmptyOptions());
        }
        return null;
    }

    private void UpdateIcon(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(X410StatusViewModel.InstalledVersion))
        {
            if (_model.InstalledVersion != null)
            {
                RootTitleBar.Tray.Icon = RootTitleBar.Icon = Icon = GetIconImage(Paths.GetAppFile());
            }
        }
    }

    #region Window
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Wpf.Ui.Appearance.Watcher.Watch(
            this,                                  // Window class
            Wpf.Ui.Appearance.BackgroundType.Mica, // Background type
            true                                   // Whether to change accents automatically
        );
    }

    private void MainWindow_Activated(object? sender, EventArgs? e)
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Topmost = true;  // important
        Topmost = false; // important
        Focus();         // important

        ShowInTaskbar = true;
    }

    private void MinimizeToTrayButton_Click(object? sender, RoutedEventArgs? e)
    {
        Hide();
        ShowInTaskbar = false;
        WindowState = WindowState.Minimized;
    }

    private void NotifyIcon_LeftDoubleClick(object sender, RoutedEventArgs e)
    {
        Show();
        Activate();
    }
    #endregion

    #region Tray ContextMenu
    private void OpenTrayMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Show();
        Activate();
    }

    private void ExitTrayMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private async void ExitAndKillX410TrayMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _model.KillAsync();
        Application.Current.Shutdown();
    }

    private void LaunchX10TrayMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _model.Launch();
    }

    private async void KillX410TrayMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _model.KillAsync();
    }
    #endregion
}
