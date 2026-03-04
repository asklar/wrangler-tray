using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WranglerTray.Models;
using WranglerTray.Services;

namespace WranglerTray.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly CloudflareAuthService _authService;
    private readonly CloudflareApiService _apiService;
    private readonly DeploymentMonitorService _monitorService;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;

    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private string _authStatusText = "Not logged in";

    [ObservableProperty]
    private AuthMode _authMode;

    [ObservableProperty]
    private string _apiTokenInput = string.Empty;

    [ObservableProperty]
    private int _pollIntervalSeconds;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _notifyOnSuccess;

    [ObservableProperty]
    private bool _notifyOnFailure;

    [ObservableProperty]
    private bool _notifyOnNewDeployment;

    [ObservableProperty]
    private bool _isWranglerInstalled;

    [ObservableProperty]
    private bool _isNpmInstalled;

    [ObservableProperty]
    private string? _wranglerVersion;

    [ObservableProperty]
    private bool _isInstallingWrangler;

    [ObservableProperty]
    private string? _installStatusMessage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private ObservableCollection<CfAccount> _accounts = [];

    [ObservableProperty]
    private CfAccount? _selectedAccount;

    public SettingsViewModel(
        CloudflareAuthService authService,
        CloudflareApiService apiService,
        DeploymentMonitorService monitorService,
        SettingsService settingsService,
        AppSettings settings)
    {
        _authService = authService;
        _apiService = apiService;
        _monitorService = monitorService;
        _settingsService = settingsService;
        _settings = settings;

        // Load current state
        IsAuthenticated = authService.IsAuthenticated;
        AuthMode = settings.AuthMode;
        PollIntervalSeconds = settings.PollIntervalSeconds;
        StartWithWindows = settings.StartWithWindows;
        NotifyOnSuccess = settings.NotifyOnSuccess;
        NotifyOnFailure = settings.NotifyOnFailure;
        NotifyOnNewDeployment = settings.NotifyOnNewDeployment;

        CheckWranglerEnvironment();

        authService.AuthStateChanged += (_, _) =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                IsAuthenticated = authService.IsAuthenticated;
                AuthMode = authService.AuthMode;
                AuthStatusText = authService.IsAuthenticated
                    ? $"Logged in ({authService.AuthMode})"
                    : "Not logged in";
            });
        };
    }

    private void CheckWranglerEnvironment()
    {
        var env = CloudflareAuthService.CheckEnvironment();
        IsWranglerInstalled = env.IsWranglerInstalled;
        IsNpmInstalled = env.IsNpmInstalled;
        WranglerVersion = env.WranglerVersion;
    }

    [RelayCommand]
    private async Task InstallWranglerAsync()
    {
        IsInstallingWrangler = true;
        InstallStatusMessage = "Installing wrangler via npm...";

        var (success, output) = await CloudflareAuthService.InstallWranglerAsync();

        if (success)
        {
            InstallStatusMessage = "✅ Wrangler installed successfully!";
            CheckWranglerEnvironment();
        }
        else
        {
            InstallStatusMessage = $"❌ Installation failed: {output}";
        }

        IsInstallingWrangler = false;
    }

    [RelayCommand]
    private async Task LoginWithWranglerAsync()
    {
        IsBusy = true;
        AuthStatusText = "Opening browser for Cloudflare login...";

        var success = await _authService.LoginWithWranglerAsync();
        if (success)
        {
            AuthStatusText = "Verifying token...";
            var valid = await _apiService.VerifyTokenAsync();
            if (valid)
            {
                _settings.AuthMode = AuthMode.WranglerLogin;
                _settingsService.Save(_settings);
                _monitorService.Start();
                await LoadAccountsAsync();
            }
            else
            {
                AuthStatusText = "Token verification failed";
                _authService.Logout();
            }
        }
        else
        {
            AuthStatusText = "Login failed or was cancelled";
        }
        IsBusy = false;
    }

    [RelayCommand]
    private async Task SaveApiTokenAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiTokenInput)) return;

        IsBusy = true;
        AuthStatusText = "Verifying API token...";

        _authService.SetApiToken(ApiTokenInput.Trim());
        var valid = await _apiService.VerifyTokenAsync();

        if (valid)
        {
            _settings.AuthMode = AuthMode.ApiToken;
            _settingsService.Save(_settings);
            AuthStatusText = "✅ API token verified";
            ApiTokenInput = string.Empty;
            _monitorService.Start();
            await LoadAccountsAsync();
        }
        else
        {
            _authService.Logout();
            AuthStatusText = "❌ Invalid API token";
        }
        IsBusy = false;
    }

    [RelayCommand]
    private void Logout()
    {
        _authService.Logout();
        _monitorService.Stop();
        _settings.AuthMode = AuthMode.None;
        _settings.SelectedAccountId = null;
        _settingsService.Save(_settings);
        Accounts.Clear();
        SelectedAccount = null;
        AuthStatusText = "Not logged in";
    }

    private async Task LoadAccountsAsync()
    {
        try
        {
            var accounts = await _apiService.GetAccountsAsync();
            Accounts = new ObservableCollection<CfAccount>(accounts);
            SelectedAccount = accounts.FirstOrDefault(a => a.Id == _settings.SelectedAccountId) ?? accounts.FirstOrDefault();
        }
        catch { }
    }

    partial void OnSelectedAccountChanged(CfAccount? value)
    {
        if (value != null)
        {
            _settings.SelectedAccountId = value.Id;
            _settingsService.Save(_settings);
        }
    }

    partial void OnPollIntervalSecondsChanged(int value)
    {
        _settings.PollIntervalSeconds = value;
        _monitorService.UpdateInterval(value);
        _settingsService.Save(_settings);
    }

    partial void OnNotifyOnSuccessChanged(bool value)
    {
        _settings.NotifyOnSuccess = value;
        _settingsService.Save(_settings);
    }

    partial void OnNotifyOnFailureChanged(bool value)
    {
        _settings.NotifyOnFailure = value;
        _settingsService.Save(_settings);
    }

    partial void OnNotifyOnNewDeploymentChanged(bool value)
    {
        _settings.NotifyOnNewDeployment = value;
        _settingsService.Save(_settings);
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        _settings.StartWithWindows = value;
        _settingsService.Save(_settings);
        SetStartupRegistry(value);
    }

    private static void SetStartupRegistry(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath;
                if (exePath != null)
                    key.SetValue("WranglerTray", $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue("WranglerTray", false);
            }
        }
        catch { }
    }
}
