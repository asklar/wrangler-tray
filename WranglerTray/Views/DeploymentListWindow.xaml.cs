using System.Windows;
using WranglerTray.ViewModels;

namespace WranglerTray.Views;

public partial class DeploymentListWindow : Window
{
    public DeploymentListWindow(DeploymentListViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        Hide();
    }
}
