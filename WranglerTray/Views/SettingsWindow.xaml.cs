using System.Windows;
using WranglerTray.ViewModels;

namespace WranglerTray.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
