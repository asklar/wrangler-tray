using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WranglerTray.Models;
using WranglerTray.Services;

namespace WranglerTray.ViewModels;

public partial class DeploymentListViewModel : ObservableObject
{
    private readonly DeploymentMonitorService _monitorService;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private List<Deployment> _allDeployments = [];

    [ObservableProperty]
    private ObservableCollection<Deployment> _deployments = [];

    [ObservableProperty]
    private ObservableCollection<string> _projectNames = [];

    [ObservableProperty]
    private string _selectedProject = "All";

    [ObservableProperty]
    private string _lastCheckedText = "Never";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isAuthenticated;

    public DeploymentListViewModel(
        DeploymentMonitorService monitorService,
        CloudflareAuthService authService,
        SettingsService settingsService,
        AppSettings settings)
    {
        _monitorService = monitorService;
        _settingsService = settingsService;
        _settings = settings;
        IsAuthenticated = authService.IsAuthenticated;

        monitorService.DeploymentsUpdated += (_, deployments) =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                UpdateDeployments(deployments);
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
            UpdateDeployments(monitorService.CurrentDeployments);
            LastCheckedText = monitorService.LastChecked?.ToLocalTime().ToString("HH:mm:ss") ?? "Never";
        }
    }

    private void UpdateDeployments(List<Deployment> deployments)
    {
        _allDeployments = deployments;

        // Build project list
        var projects = deployments
            .Select(d => d.ProjectName)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        var newNames = new List<string> { "All" };
        newNames.AddRange(projects);
        ProjectNames = new ObservableCollection<string>(newNames);

        // Restore last selected project, or stay on current
        if (!string.IsNullOrEmpty(_settings.LastSelectedProject) &&
            newNames.Contains(_settings.LastSelectedProject) &&
            SelectedProject == "All")
        {
            SelectedProject = _settings.LastSelectedProject;
        }
        else if (!newNames.Contains(SelectedProject))
        {
            SelectedProject = "All";
        }

        ApplyFilter();
    }

    partial void OnSelectedProjectChanged(string value)
    {
        _settings.LastSelectedProject = value;
        _settingsService.Save(_settings);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filtered = SelectedProject == "All"
            ? _allDeployments
            : _allDeployments.Where(d => d.ProjectName == SelectedProject).ToList();

        Deployments = new ObservableCollection<Deployment>(filtered);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await _monitorService.PollAsync();
    }

    [RelayCommand]
    private static void OpenDeployment(Deployment? deployment)
    {
        var url = deployment?.DashboardUrl;
        if (string.IsNullOrEmpty(url)) return;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
        {
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private static void Exit()
    {
        System.Windows.Application.Current?.Shutdown();
    }

    public event EventHandler? OpenSettingsRequested;

    [RelayCommand]
    private void OpenSettings()
    {
        OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
    }
}
