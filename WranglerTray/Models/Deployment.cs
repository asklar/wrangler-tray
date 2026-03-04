namespace WranglerTray.Models;

public enum DeploymentType
{
    Worker,
    Pages
}

public enum DeploymentStatus
{
    Unknown,
    Queued,
    Active,
    Success,
    Failure,
    Canceled
}

public class Deployment
{
    public string Id { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public DeploymentType Type { get; set; }
    public DeploymentStatus Status { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public string? CommitHash { get; set; }
    public string? CommitMessage { get; set; }
    public string? Url { get; set; }
    public string? DashboardUrl { get; set; }
    public string? Environment { get; set; }

    public string ShortCommitHash => CommitHash?.Length > 7 ? CommitHash[..7] : CommitHash ?? "";

    public string TimeAgo
    {
        get
        {
            var elapsed = DateTime.UtcNow - CreatedOn;
            if (elapsed.TotalSeconds < 60) return $"{(int)elapsed.TotalSeconds}s ago";
            if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
            if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
            return $"{(int)elapsed.TotalDays}d ago";
        }
    }
}
