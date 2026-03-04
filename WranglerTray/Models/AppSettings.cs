using System.Text.Json.Serialization;

namespace WranglerTray.Models;

public enum AuthMode
{
    None,
    WranglerLogin,
    ApiToken
}

public class AppSettings
{
    [JsonPropertyName("authMode")]
    public AuthMode AuthMode { get; set; } = AuthMode.None;

    [JsonPropertyName("pollIntervalSeconds")]
    public int PollIntervalSeconds { get; set; } = 30;

    [JsonPropertyName("startWithWindows")]
    public bool StartWithWindows { get; set; } = false;

    [JsonPropertyName("notifyOnSuccess")]
    public bool NotifyOnSuccess { get; set; } = true;

    [JsonPropertyName("notifyOnFailure")]
    public bool NotifyOnFailure { get; set; } = true;

    [JsonPropertyName("notifyOnNewDeployment")]
    public bool NotifyOnNewDeployment { get; set; } = false;

    [JsonPropertyName("selectedAccountId")]
    public string? SelectedAccountId { get; set; }

    [JsonPropertyName("lastSelectedProject")]
    public string? LastSelectedProject { get; set; }
}
