using WranglerTray.Models;

namespace WranglerTray.Services;

public class DeploymentMonitorService : IDisposable
{
    private readonly CloudflareApiService _apiService;
    private readonly CloudflareAuthService _authService;
    private readonly NotificationService _notificationService;
    private readonly AppSettings _settings;
    private System.Timers.Timer? _timer;

    private Dictionary<string, Deployment> _knownDeployments = new();
    private bool _isPolling;

    public event EventHandler<List<Deployment>>? DeploymentsUpdated;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<bool>? PollingStateChanged;

    public DateTime? LastChecked { get; private set; }
    public List<Deployment> CurrentDeployments { get; private set; } = [];

    public DeploymentMonitorService(
        CloudflareApiService apiService,
        CloudflareAuthService authService,
        NotificationService notificationService,
        AppSettings settings)
    {
        _apiService = apiService;
        _authService = authService;
        _notificationService = notificationService;
        _settings = settings;
    }

    public void Start()
    {
        if (_timer != null) return;
        _timer = new System.Timers.Timer(_settings.PollIntervalSeconds * 1000);
        _timer.Elapsed += async (_, _) => await PollAsync();
        _timer.Start();
        // Do an immediate poll
        _ = PollAsync();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    public void UpdateInterval(int seconds)
    {
        if (_timer != null)
            _timer.Interval = seconds * 1000;
    }

    public async Task PollAsync()
    {
        if (_isPolling || !_authService.IsAuthenticated) return;
        _isPolling = true;
        PollingStateChanged?.Invoke(this, true);

        try
        {
            // Refresh token once (may do file I/O or subprocess), off UI thread
            await Task.Run(() => _authService.RefreshTokenIfNeeded());

            var allDeployments = new List<Deployment>();
            var accountId = _settings.SelectedAccountId;

            if (string.IsNullOrEmpty(accountId))
            {
                // Discover accounts
                var accounts = await _apiService.GetAccountsAsync();
                if (accounts.Count == 0)
                {
                    ErrorOccurred?.Invoke(this, "No Cloudflare accounts found.");
                    return;
                }
                accountId = accounts[0].Id;
                _settings.SelectedAccountId = accountId;
            }

            // Fetch Workers deployments
            try
            {
                var workers = await _apiService.GetWorkersAsync(accountId);
                foreach (var worker in workers)
                {
                    var deployments = await _apiService.GetWorkerDeploymentsAsync(accountId, worker.Id);
                    foreach (var d in deployments)
                    {
                        allDeployments.Add(new Deployment
                        {
                            Id = $"worker-{worker.Id}-{d.Id}",
                            ProjectName = worker.Id,
                            Type = DeploymentType.Worker,
                            Status = DeploymentStatus.Success, // Workers deployments are always "deployed"
                            CreatedOn = d.CreatedOn,
                            DashboardUrl = $"https://dash.cloudflare.com/{accountId}/workers/services/view/{worker.Id}"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Workers API error: {ex.Message}");
            }

            // Fetch Pages deployments
            try
            {
                var projects = await _apiService.GetPagesProjectsAsync(accountId);
                foreach (var project in projects)
                {
                    var deployments = await _apiService.GetPagesDeploymentsAsync(accountId, project.Name);
                    foreach (var d in deployments)
                    {
                        allDeployments.Add(new Deployment
                        {
                            Id = $"pages-{project.Name}-{d.Id}",
                            ProjectName = project.Name,
                            Type = DeploymentType.Pages,
                            Status = MapPagesStatus(d.LatestStage?.Status),
                            CreatedOn = d.CreatedOn,
                            ModifiedOn = d.ModifiedOn,
                            CommitHash = d.DeploymentTrigger?.Metadata?.CommitHash,
                            CommitMessage = d.DeploymentTrigger?.Metadata?.CommitMessage,
                            Url = d.Url,
                            Environment = d.Environment,
                            DashboardUrl = $"https://dash.cloudflare.com/{accountId}/pages/view/{project.Name}/{d.Id}"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Pages API error: {ex.Message}");
            }

            // Diff and notify
            ProcessDeploymentChanges(allDeployments);

            CurrentDeployments = allDeployments
                .OrderByDescending(d => d.CreatedOn)
                .Take(30)
                .ToList();

            LastChecked = DateTime.UtcNow;
            DeploymentsUpdated?.Invoke(this, CurrentDeployments);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex.Message);
        }
        finally
        {
            _isPolling = false;
            PollingStateChanged?.Invoke(this, false);
        }
    }

    private void ProcessDeploymentChanges(List<Deployment> fresh)
    {
        foreach (var d in fresh)
        {
            if (_knownDeployments.TryGetValue(d.Id, out var known))
            {
                if (known.Status != d.Status)
                {
                    _notificationService.NotifyDeploymentChanged(d, known.Status);
                }
            }
            else if (_knownDeployments.Count > 0) // Don't notify on first load
            {
                _notificationService.NotifyNewDeployment(d);
            }
        }

        _knownDeployments = fresh.ToDictionary(d => d.Id);
    }

    private static DeploymentStatus MapPagesStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "idle" => DeploymentStatus.Queued,
        "active" => DeploymentStatus.Active,
        "success" => DeploymentStatus.Success,
        "failure" => DeploymentStatus.Failure,
        "canceled" => DeploymentStatus.Canceled,
        _ => DeploymentStatus.Unknown
    };

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
