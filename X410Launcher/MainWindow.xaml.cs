using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using X410Launcher.ViewModels;

namespace X410Launcher;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly X410StatusViewModel _model = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _model;

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
}
