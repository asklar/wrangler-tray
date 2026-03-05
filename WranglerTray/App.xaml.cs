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

    private static readonly string LogPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WranglerTray", "error.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception handlers
        DispatcherUnhandledException += (_, args) =>
        {
            LogError("DispatcherUnhandled", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                LogError("AppDomainUnhandled", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogError("UnobservedTask", args.Exception);
            args.SetObserved();
        };

        try
        {
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
        catch (Exception ex)
        {
            LogError("Startup", ex);
            System.Windows.MessageBox.Show(
                $"WranglerTray failed to start:\n\n{ex.Message}\n\nSee {LogPath} for details.",
                "WranglerTray Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private static void LogError(string context, Exception ex)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(LogPath)!;
            System.IO.Directory.CreateDirectory(dir);
            var entry = $"[{DateTime.UtcNow:o}] [{context}] {ex}\n\n";
            System.IO.File.AppendAllText(LogPath, entry);
        }
        catch { }
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
            var vm = new DeploymentListViewModel(_monitorService, _authService, _settingsService, _settings);
            vm.OpenSettingsRequested += (_, _) => ShowSettings();
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
        // Load embedded icon from build output directory
        var exeDir = AppContext.BaseDirectory;
        var icoPath = System.IO.Path.Combine(exeDir, "Assets", "tray-icon.ico");
        if (System.IO.File.Exists(icoPath))
            return new Icon(icoPath, 16, 16);

        // Try WPF resource stream
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/tray-icon.ico");
            var sri = Application.GetResourceStream(uri);
            if (sri?.Stream != null)
            {
                using var stream = sri.Stream;
                return new Icon(stream, 16, 16);
            }
        }
        catch { }

        // Fallback: bold orange circle
        var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Color.FromArgb(249, 115, 22));
            g.FillEllipse(brush, 1, 1, 14, 14);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    private void ExitApp()
    {
        _monitorService.Dispose();
        _trayIcon?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitorService?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}

