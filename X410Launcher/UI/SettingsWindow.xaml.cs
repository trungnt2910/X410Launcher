using MahApps.Metro.Controls;
using System.Windows;
using X410Launcher.ViewModels;

namespace X410Launcher;

/// <summary>
/// Interaction logic for SettingsWindow.xaml
/// </summary>
public partial class SettingsWindow : MetroWindow
{
    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = new SettingsViewModel();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
