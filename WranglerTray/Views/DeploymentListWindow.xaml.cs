using System.Windows;
using System.Windows.Input;
using WranglerTray.Models;
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

    private void DeploymentList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DeploymentList.SelectedItem is Deployment deployment && DataContext is DeploymentListViewModel vm)
        {
            vm.OpenDeploymentCommand.Execute(deployment);
        }
    }
}
