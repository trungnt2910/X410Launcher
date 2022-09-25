using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X410Launcher.Tools;
using X410Launcher.ViewModels;
using WinIcon = System.Drawing.Icon;

namespace X410Launcher;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly X410StatusViewModel _model;

    public MainWindow()
    {
        InitializeComponent();

        Activated += MainWindow_Activated;

        // Loaded from StaticResource
        _model = (X410StatusViewModel)DataContext;

        if (Environment.GetCommandLineArgs().Contains("--tray"))
        {
            MinimizeToTrayButton_Click(null, null);
        }

        RefreshButton_Click(null, null);
    }

    private void DisableButtons()
    {
        RefreshButton.IsEnabled = false;
        InstallButton.IsEnabled = false;
        UninstallButton.IsEnabled = false;
        LaunchButton.IsEnabled = false;
        KillButton.IsEnabled = false;
    }

    private void EnableButtons()
    {
        RefreshButton.IsEnabled = true;
        InstallButton.IsEnabled = _model.Packages.Any();
        UninstallButton.IsEnabled = _model.InstalledVersion != null;
        LaunchButton.IsEnabled = _model.InstalledVersion != null;
        KillButton.IsEnabled = _model.InstalledVersion != null;
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

    private async void RefreshButton_Click(object? sender, RoutedEventArgs? e)
    {
        DisableButtons();

        try
        {
            await _model.RefreshAsync();
            if (_model.Packages.Any())
            {
                PackagesDataGrid.SelectedIndex = 0;
            }
            if (_model.InstalledVersion != null)
            {
                var appFile = Paths.GetAppFile();
                Icon = GetIconImage(appFile) ?? Icon;
                NotifyIcon.Icon?.Dispose();
                NotifyIcon.Icon = GetIcon(appFile);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Failed to fetch packages", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        EnableButtons();
    }

    private void ApiHyperlink_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(_model.Api);
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        DisableButtons();

        try
        {
            await _model.InstallPackageAsync(PackagesDataGrid.SelectedIndex);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Failed to install packages", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        EnableButtons();
    }

    private async void UninstallButton_Click(object sender, RoutedEventArgs e)
    {
        DisableButtons();

        try
        {
            await _model.UninstallPackageAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Failed to uninstall packages", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        EnableButtons();
    }

    private async void KillButton_Click(object sender, RoutedEventArgs e)
    {
        DisableButtons();

        try
        {
            await _model.KillAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Failed to kill X410", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        EnableButtons();
    }

    private void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        DisableButtons();

        try
        {
            _model.Launch();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Failed to start X410", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        EnableButtons();
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow()
        {
            Owner = this
        };
        window.ShowDialog();
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

    private void NotifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        Activate();
    }
}
