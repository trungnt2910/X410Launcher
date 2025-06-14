using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using X410Launcher.Tools;
using X410Launcher.UI.Pages;
using X410Launcher.ViewModels;
using WinIcon = System.Drawing.Icon;

namespace X410Launcher;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow
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
                Tray.Icon = Icon = GetIconImage(Paths.GetAppFile()) ?? Icon;
                RootTitleBar.Icon = new ImageIcon() { Source = Icon };
            }
        }
    }

    private async Task SafeKillAsync()
    {
        if (!await _model.KillAsync())
        {
            var result = System.Windows.MessageBox.Show(
                "There are active X clients. Are you sure you want to kill X410? " +
                "You may lose any unsaved work.",
                "Kill X410",
                System.Windows.MessageBoxButton.YesNo
            );

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                await _model.KillAsync(force: true);
            }
        }
    }

    #region Window
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Weird bug when WPFUI does not apply the system theme
        // when a window is already being watched:
        // https://github.com/lepoco/wpfui/blob/7535b9135b24acae83b7097023928a8efbd153aa/src/Wpf.Ui/Appearance/SystemThemeWatcher.cs#L56
        ApplicationThemeManager.ApplySystemTheme(true);
        SystemThemeWatcher.Watch(
            this,                                  // Window class
            WindowBackdropType.Mica,               // Background type
            true                                   // Whether to change accents automatically
        );

        // WPFUI v3 NavigationView does not select any page by default.
        RootNavigation.Navigate(typeof(HomePage));
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

    private void MinimizeToTrayButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        MinimizeToTrayButton.Hover();
    }

    private void MinimizeToTrayButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        MinimizeToTrayButton.RemoveHover();
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
        await SafeKillAsync();
        Application.Current.Shutdown();
    }

    private async void LaunchX10TrayMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _model.LaunchAsync();
    }

    private async void KillX410TrayMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await SafeKillAsync();
    }
    #endregion
}
