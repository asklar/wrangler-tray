using System.Net;
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

    public async Task<List<CfAccount>> GetAccountsAsync()
        => (await GetAsync<CloudflareResponse<List<CfAccount>>>("accounts?per_page=50"))?.Result ?? [];

    /// <summary>
    /// Get the signed-in user's email (identity). Returns null if unavailable
    /// (e.g. an API token without user:read permission).
    /// </summary>
    public async Task<string?> GetUserEmailAsync()
    {
        try
        {
            return (await GetAsync<CloudflareResponse<CfUser>>("user"))?.Result?.Email;
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<CfWorkerScript>> GetWorkersAsync(string accountId)
        => (await GetAsync<CloudflareResponse<List<CfWorkerScript>>>(
            $"accounts/{accountId}/workers/scripts"))?.Result ?? [];

    public async Task<List<CfWorkerDeployment>> GetWorkerDeploymentsAsync(string accountId, string scriptName)
        => (await GetAsync<CloudflareResponse<CfWorkerDeploymentsResult>>(
            $"accounts/{accountId}/workers/scripts/{scriptName}/deployments"))?.Result?.Items ?? [];

    public async Task<List<CfPagesProject>> GetPagesProjectsAsync(string accountId)
        => (await GetAsync<CloudflareResponse<List<CfPagesProject>>>(
            $"accounts/{accountId}/pages/projects"))?.Result ?? [];

    public async Task<List<CfPagesDeployment>> GetPagesDeploymentsAsync(string accountId, string projectName)
        => (await GetAsync<CloudflareResponse<List<CfPagesDeployment>>>(
            $"accounts/{accountId}/pages/projects/{projectName}/deployments"))?.Result ?? [];

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

    private async Task<T?> GetAsync<T>(string path)
    {
        using var response = await SendWithRetryAsync(path);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    /// <summary>
    /// Send a GET request. If it returns 401 while using wrangler login, force a
    /// token refresh and retry once; if it still fails, mark auth as lost so the
    /// UI prompts for re-login.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithRetryAsync(string path)
    {
        var response = await SendOnceAsync(path);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        if (_authService.AuthMode == AuthMode.WranglerLogin)
        {
            response.Dispose();
            if (await _authService.ForceRefreshTokenAsync())
            {
                response = await SendOnceAsync(path);
                if (response.StatusCode != HttpStatusCode.Unauthorized)
                    return response;
            }
            _authService.MarkAuthLost();
        }

        return response;
    }

    private async Task<HttpResponseMessage> SendOnceAsync(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        var token = _authService.GetAccessToken();
        if (token != null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _httpClient.SendAsync(request);
    }
}
