using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tomlyn;
using Tomlyn.Model;
using WranglerTray.Models;

namespace WranglerTray.Services;

public class CloudflareAuthService
{
    private string? _cachedToken;
    private AuthMode _authMode = AuthMode.None;

    private static readonly string CredentialTarget = "WranglerTray_ApiToken";
    private static readonly string WranglerConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".wrangler", "config", "default.toml");

    public AuthMode AuthMode => _authMode;
    public bool IsAuthenticated => _authMode != AuthMode.None && _cachedToken != null;

    public event EventHandler? AuthStateChanged;

    /// <summary>
    /// Try to restore auth from persisted state (wrangler config or credential manager).
    /// </summary>
    public void TryRestoreAuth(AppSettings settings)
    {
        _authMode = settings.AuthMode;
        switch (_authMode)
        {
            case AuthMode.WranglerLogin:
                _cachedToken = ReadWranglerToken();
                break;
            case AuthMode.ApiToken:
                _cachedToken = ReadCredentialManager();
                break;
        }
        if (_cachedToken == null)
            _authMode = AuthMode.None;
    }

    /// <summary>
    /// Create a ProcessStartInfo that runs a command through cmd.exe,
    /// which is required on Windows for .cmd/.bat scripts like wrangler and npm.
    /// </summary>
    private static ProcessStartInfo CmdPsi(string command, string args, bool redirect = true)
    {
        return new ProcessStartInfo("cmd.exe", $"/c {command} {args}")
        {
            RedirectStandardOutput = redirect,
            RedirectStandardError = redirect,
            UseShellExecute = !redirect,
            CreateNoWindow = true
        };
    }

    /// <summary>
    /// Detect if wrangler CLI is available on PATH.
    /// </summary>
    public static bool IsWranglerAvailable()
    {
        try
        {
            using var proc = Process.Start(CmdPsi("wrangler", "--version"));
            proc?.WaitForExit(10000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Detect if npm is available on PATH.
    /// </summary>
    public static bool IsNpmAvailable()
    {
        try
        {
            using var proc = Process.Start(CmdPsi("npm", "--version"));
            proc?.WaitForExit(10000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get the installed wrangler version string, or null if not available.
    /// </summary>
    public static string? GetWranglerVersion()
    {
        try
        {
            using var proc = Process.Start(CmdPsi("wrangler", "--version"));
            if (proc == null) return null;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(10000);
            return proc.ExitCode == 0 ? output.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Install wrangler globally via npm. Returns true on success.
    /// </summary>
    public static async Task<(bool Success, string Output)> InstallWranglerAsync()
    {
        try
        {
            using var proc = Process.Start(CmdPsi("npm", "install -g wrangler"));
            if (proc == null) return (false, "Failed to start npm process.");

            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            var output = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
            return (proc.ExitCode == 0, output.Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Returns a status summary of wrangler/npm availability.
    /// </summary>
    public static WranglerEnvironmentStatus CheckEnvironment()
    {
        var status = new WranglerEnvironmentStatus
        {
            IsWranglerInstalled = IsWranglerAvailable(),
            IsNpmInstalled = IsNpmAvailable()
        };
        if (status.IsWranglerInstalled)
            status.WranglerVersion = GetWranglerVersion();
        return status;
    }

    /// <summary>
    /// Launch `wrangler login` and wait for user to complete the flow.
    /// </summary>
    public async Task<bool> LoginWithWranglerAsync()
    {
        try
        {
            var psi = new ProcessStartInfo("cmd.exe", "/c wrangler login")
            {
                UseShellExecute = true,
                CreateNoWindow = false
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;

            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0) return false;

            var token = ReadWranglerToken();
            if (token == null) return false;

            _cachedToken = token;
            _authMode = AuthMode.WranglerLogin;
            AuthStateChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Set an API token from the settings UI.
    /// </summary>
    public void SetApiToken(string token)
    {
        WriteCredentialManager(token);
        _cachedToken = token;
        _authMode = AuthMode.ApiToken;
        AuthStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Get the current access token for API calls.
    /// </summary>
    public string? GetAccessToken()
    {
        if (_authMode == AuthMode.WranglerLogin)
        {
            // Re-read in case wrangler refreshed it
            var fresh = ReadWranglerToken();
            if (fresh != null) _cachedToken = fresh;
        }
        return _cachedToken;
    }

    public void Logout()
    {
        if (_authMode == AuthMode.ApiToken)
            DeleteCredentialManager();

        _cachedToken = null;
        _authMode = AuthMode.None;
        AuthStateChanged?.Invoke(this, EventArgs.Empty);
    }

    #region Wrangler Token Reading

    private static string? ReadWranglerToken()
    {
        try
        {
            if (!File.Exists(WranglerConfigPath)) return null;

            var toml = File.ReadAllText(WranglerConfigPath);
            var model = Toml.ToModel(toml);

            if (model.TryGetValue("oauth_token", out var token))
                return token?.ToString();

            return null;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Windows Credential Manager (DPAPI)

    private static readonly string CredFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WranglerTray", "cred.dat");

    private static void WriteCredentialManager(string token)
    {
        var dir = Path.GetDirectoryName(CredFilePath)!;
        Directory.CreateDirectory(dir);

        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(token),
            Encoding.UTF8.GetBytes(CredentialTarget),
            DataProtectionScope.CurrentUser);

        File.WriteAllBytes(CredFilePath, encrypted);
    }

    private static string? ReadCredentialManager()
    {
        try
        {
            if (!File.Exists(CredFilePath)) return null;

            var encrypted = File.ReadAllBytes(CredFilePath);
            var decrypted = ProtectedData.Unprotect(
                encrypted,
                Encoding.UTF8.GetBytes(CredentialTarget),
                DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return null;
        }
    }

    private static void DeleteCredentialManager()
    {
        try { File.Delete(CredFilePath); } catch { }
    }

    #endregion
}

public class WranglerEnvironmentStatus
{
    public bool IsWranglerInstalled { get; set; }
    public bool IsNpmInstalled { get; set; }
    public string? WranglerVersion { get; set; }

    public bool CanInstallWrangler => !IsWranglerInstalled && IsNpmInstalled;
    public bool NeedsNodeJs => !IsWranglerInstalled && !IsNpmInstalled;
}
