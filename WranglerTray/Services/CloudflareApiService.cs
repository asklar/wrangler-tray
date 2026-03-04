using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using WranglerTray.Models;

namespace WranglerTray.Services;

public class CloudflareApiService
{
    private readonly CloudflareAuthService _authService;
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CloudflareApiService(CloudflareAuthService authService)
    {
        _authService = authService;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.cloudflare.com/client/v4/")
        };
    }

    private void SetAuth()
    {
        var token = _authService.GetAccessToken();
        if (token != null)
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<List<CfAccount>> GetAccountsAsync()
    {
        SetAuth();
        var response = await _httpClient.GetAsync("accounts?per_page=50");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<CloudflareResponse<List<CfAccount>>>(json, JsonOptions);
        return result?.Result ?? [];
    }

    public async Task<List<CfWorkerScript>> GetWorkersAsync(string accountId)
    {
        SetAuth();
        var response = await _httpClient.GetAsync($"accounts/{accountId}/workers/scripts");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<CloudflareResponse<List<CfWorkerScript>>>(json, JsonOptions);
        return result?.Result ?? [];
    }

    public async Task<List<CfWorkerDeployment>> GetWorkerDeploymentsAsync(string accountId, string scriptName)
    {
        SetAuth();
        var response = await _httpClient.GetAsync($"accounts/{accountId}/workers/scripts/{scriptName}/deployments");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<CloudflareResponse<CfWorkerDeploymentsResult>>(json, JsonOptions);
        return result?.Result?.Items ?? [];
    }

    public async Task<List<CfPagesProject>> GetPagesProjectsAsync(string accountId)
    {
        SetAuth();
        var response = await _httpClient.GetAsync($"accounts/{accountId}/pages/projects?per_page=25");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<CloudflareResponse<List<CfPagesProject>>>(json, JsonOptions);
        return result?.Result ?? [];
    }

    public async Task<List<CfPagesDeployment>> GetPagesDeploymentsAsync(string accountId, string projectName)
    {
        SetAuth();
        var response = await _httpClient.GetAsync(
            $"accounts/{accountId}/pages/projects/{projectName}/deployments?per_page=10");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<CloudflareResponse<List<CfPagesDeployment>>>(json, JsonOptions);
        return result?.Result ?? [];
    }

    /// <summary>
    /// Verify the token works by fetching accounts.
    /// </summary>
    public async Task<bool> VerifyTokenAsync()
    {
        try
        {
            var accounts = await GetAccountsAsync();
            return accounts.Count > 0;
        }
        catch
        {
            return false;
        }
    }
}
