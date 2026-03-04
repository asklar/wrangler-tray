using System.Text.Json.Serialization;

namespace WranglerTray.Models;

// Generic Cloudflare API response wrapper
public class CloudflareResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    public T? Result { get; set; }

    [JsonPropertyName("errors")]
    public List<CloudflareError>? Errors { get; set; }

    [JsonPropertyName("result_info")]
    public ResultInfo? ResultInfo { get; set; }
}

public class CloudflareError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class ResultInfo
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("per_page")]
    public int PerPage { get; set; }

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }
}

// Accounts
public class CfAccount
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

// Workers
public class CfWorkerScript
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("created_on")]
    public DateTime? CreatedOn { get; set; }

    [JsonPropertyName("modified_on")]
    public DateTime? ModifiedOn { get; set; }
}

public class CfWorkerDeployment
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("strategy")]
    public string? Strategy { get; set; }

    [JsonPropertyName("created_on")]
    public DateTime CreatedOn { get; set; }

    [JsonPropertyName("versions")]
    public List<CfWorkerDeploymentVersion>? Versions { get; set; }
}

public class CfWorkerDeploymentVersion
{
    [JsonPropertyName("version_id")]
    public string? VersionId { get; set; }

    [JsonPropertyName("percentage")]
    public double Percentage { get; set; }
}

// Pages
public class CfPagesProject
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("subdomain")]
    public string? Subdomain { get; set; }

    [JsonPropertyName("created_on")]
    public DateTime? CreatedOn { get; set; }
}

public class CfPagesDeployment
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("project_name")]
    public string? ProjectName { get; set; }

    [JsonPropertyName("environment")]
    public string? Environment { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("created_on")]
    public DateTime CreatedOn { get; set; }

    [JsonPropertyName("modified_on")]
    public DateTime? ModifiedOn { get; set; }

    [JsonPropertyName("latest_stage")]
    public CfPagesStage? LatestStage { get; set; }

    [JsonPropertyName("deployment_trigger")]
    public CfPagesDeploymentTrigger? DeploymentTrigger { get; set; }

    [JsonPropertyName("source")]
    public CfPagesSource? Source { get; set; }
}

public class CfPagesStage
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

public class CfPagesDeploymentTrigger
{
    [JsonPropertyName("metadata")]
    public CfPagesDeploymentMetadata? Metadata { get; set; }
}

public class CfPagesDeploymentMetadata
{
    [JsonPropertyName("commit_hash")]
    public string? CommitHash { get; set; }

    [JsonPropertyName("commit_message")]
    public string? CommitMessage { get; set; }

    [JsonPropertyName("branch")]
    public string? Branch { get; set; }
}

public class CfPagesSource
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

// Workers deployments response has a different shape
public class CfWorkerDeploymentsResult
{
    [JsonPropertyName("items")]
    public List<CfWorkerDeployment>? Items { get; set; }
}
