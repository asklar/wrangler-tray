namespace WranglerTray.Models;

public class Project
{
    public string Name { get; set; } = string.Empty;
    public DeploymentType Type { get; set; }
    public string AccountId { get; set; } = string.Empty;
    public string? Subdomain { get; set; }
    public DateTime? LatestDeploymentAt { get; set; }
}
