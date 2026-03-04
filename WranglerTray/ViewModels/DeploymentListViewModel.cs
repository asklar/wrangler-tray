using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WranglerTray.Models;
using WranglerTray.Services;

namespace WranglerTray.ViewModels;

public partial class DeploymentListViewModel : ObservableObject
{
    private readonly DeploymentMonitorService _monitorService;

    [ObservableProperty]
    private ObservableCollection<Deployment> _deployments = [];

    [ObservableProperty]
    private string _lastCheckedText = "Never";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isAuthenticated;

    public DeploymentListViewModel(DeploymentMonitorService monitorService, CloudflareAuthService authService)
    {
        _monitorService = monitorService;
        IsAuthenticated = authService.IsAuthenticated;

        monitorService.DeploymentsUpdated += (_, deployments) =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                Deployments = new ObservableCollection<Deployment>(deployments);
                LastCheckedText = monitorService.LastChecked?.ToLocalTime().ToString("HH:mm:ss") ?? "Never";
                ErrorMessage = null;
            });
        };

        monitorService.PollingStateChanged += (_, isPolling) =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() => IsLoading = isPolling);
        };

        monitorService.ErrorOccurred += (_, error) =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() => ErrorMessage = error);
        };

        authService.AuthStateChanged += (_, _) =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                IsAuthenticated = authService.IsAuthenticated);
        };

        // Load existing data
        if (monitorService.CurrentDeployments.Count > 0)
        {
            Deployments = new ObservableCollection<Deployment>(monitorService.CurrentDeployments);
            LastCheckedText = monitorService.LastChecked?.ToLocalTime().ToString("HH:mm:ss") ?? "Never";
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await _monitorService.PollAsync();
    }
}
