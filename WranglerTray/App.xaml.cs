using System.Drawing;
using System.Windows;
using WranglerTray.Models;
using WranglerTray.Services;
using WranglerTray.ViewModels;
using WranglerTray.Views;
using Forms = System.Windows.Forms;
using Application = System.Windows.Application;

namespace WranglerTray;

public partial class App : Application
{
    private Forms.NotifyIcon? _trayIcon;
    private SettingsService _settingsService = null!;
    private AppSettings _settings = null!;
    private CloudflareAuthService _authService = null!;
    private CloudflareApiService _apiService = null!;
    private NotificationService _notificationService = null!;
    private DeploymentMonitorService _monitorService = null!;
    private DeploymentListWindow? _deploymentWindow;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single-instance check
        var mutex = new System.Threading.Mutex(true, "WranglerTray_SingleInstance", out bool isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }
        GC.KeepAlive(mutex);

        // Initialize services
        _settingsService = new SettingsService();
        _settings = _settingsService.Load();
        _authService = new CloudflareAuthService();
        _apiService = new CloudflareApiService(_authService);
        _notificationService = new NotificationService(_settings);
        _monitorService = new DeploymentMonitorService(_apiService, _authService, _notificationService, _settings);

        // Try restore auth
        _authService.TryRestoreAuth(_settings);

        // Set up tray icon
        SetupTrayIcon();

        // Start monitoring if authenticated
        if (_authService.IsAuthenticated)
            _monitorService.Start();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new Forms.NotifyIcon
        {
            Text = "Wrangler Tray — Cloudflare Deployment Monitor",
            Icon = CreateDefaultIcon(),
            Visible = true,
            ContextMenuStrip = CreateContextMenu()
        };

        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
                ShowDeploymentList();
        };

        // Update icon based on monitor state
        _monitorService.PollingStateChanged += (_, isPolling) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_trayIcon != null)
                    _trayIcon.Text = isPolling ? "Wrangler Tray — Checking..." : "Wrangler Tray";
            });
        };

        _monitorService.ErrorOccurred += (_, error) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_trayIcon != null)
                    _trayIcon.Text = $"Wrangler Tray — Error: {error[..Math.Min(error.Length, 50)]}";
            });
        };
    }

    private Forms.ContextMenuStrip CreateContextMenu()
    {
        var menu = new Forms.ContextMenuStrip();

        var deploymentsItem = new Forms.ToolStripMenuItem("📋 Deployments");
        deploymentsItem.Click += (_, _) => ShowDeploymentList();
        menu.Items.Add(deploymentsItem);

        var settingsItem = new Forms.ToolStripMenuItem("⚙️ Settings");
        settingsItem.Click += (_, _) => ShowSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new Forms.ToolStripSeparator());

        var exitItem = new Forms.ToolStripMenuItem("❌ Exit");
        exitItem.Click += (_, _) => ExitApp();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void ShowDeploymentList()
    {
        if (_deploymentWindow == null || !_deploymentWindow.IsLoaded)
        {
            var vm = new DeploymentListViewModel(_monitorService, _authService);
            _deploymentWindow = new DeploymentListWindow(vm);
        }

        PositionNearTray(_deploymentWindow);
        _deploymentWindow.Show();
        _deploymentWindow.Activate();
    }

    private void ShowSettings()
    {
        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            var vm = new SettingsViewModel(_authService, _apiService, _monitorService, _settingsService, _settings);
            _settingsWindow = new SettingsWindow(vm);
        }

        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private static void PositionNearTray(Window window)
    {
        var workArea = SystemParameters.WorkArea;
        window.Left = workArea.Right - window.Width - 8;
        window.Top = workArea.Bottom - window.Height - 8;
    }

    private static Icon CreateDefaultIcon()
    {
        // Create a simple 16x16 icon with a cloud shape
        var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Color.FromArgb(249, 115, 22)); // Orange/Cloudflare color
            g.FillEllipse(brush, 1, 4, 14, 10);
            g.FillEllipse(brush, 3, 1, 10, 10);
        }
        var handle = bmp.GetHicon();
        return Icon.FromHandle(handle);
    }

    private void ExitApp()
    {
        _monitorService.Dispose();
        _trayIcon?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitorService.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}

