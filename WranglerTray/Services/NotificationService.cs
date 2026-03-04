using Microsoft.Toolkit.Uwp.Notifications;
using WranglerTray.Models;

namespace WranglerTray.Services;

public class NotificationService
{
    private readonly AppSettings _settings;

    public NotificationService(AppSettings settings)
    {
        _settings = settings;
    }

    public void NotifyDeploymentChanged(Deployment deployment, DeploymentStatus previousStatus)
    {
        // Check if we should notify based on settings
        if (deployment.Status == DeploymentStatus.Success && !_settings.NotifyOnSuccess) return;
        if (deployment.Status == DeploymentStatus.Failure && !_settings.NotifyOnFailure) return;

        var typeBadge = deployment.Type == DeploymentType.Worker ? "⚡ Worker" : "📄 Pages";
        var statusEmoji = deployment.Status switch
        {
            DeploymentStatus.Success => "✅",
            DeploymentStatus.Failure => "❌",
            DeploymentStatus.Active => "🔄",
            DeploymentStatus.Queued => "⏳",
            DeploymentStatus.Canceled => "🚫",
            _ => "❓"
        };

        var title = $"{statusEmoji} {deployment.ProjectName}";
        var body = $"{typeBadge} — {deployment.Status}";

        if (!string.IsNullOrEmpty(deployment.CommitMessage))
            body += $"\n{deployment.ShortCommitHash}: {Truncate(deployment.CommitMessage, 80)}";

        var builder = new ToastContentBuilder()
            .AddText(title)
            .AddText(body);

        if (!string.IsNullOrEmpty(deployment.DashboardUrl))
            builder.AddArgument("url", deployment.DashboardUrl);

        builder.Show();
    }

    public void NotifyNewDeployment(Deployment deployment)
    {
        if (!_settings.NotifyOnNewDeployment) return;

        var typeBadge = deployment.Type == DeploymentType.Worker ? "⚡ Worker" : "📄 Pages";
        var title = $"🆕 New deployment: {deployment.ProjectName}";
        var body = $"{typeBadge} — {deployment.Status}";

        if (!string.IsNullOrEmpty(deployment.CommitMessage))
            body += $"\n{deployment.ShortCommitHash}: {Truncate(deployment.CommitMessage, 80)}";

        new ToastContentBuilder()
            .AddText(title)
            .AddText(body)
            .Show();
    }

    public void NotifyError(string message)
    {
        new ToastContentBuilder()
            .AddText("⚠️ Wrangler Tray")
            .AddText(message)
            .Show();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
    }
}
