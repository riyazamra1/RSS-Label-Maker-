using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RSS.LabelMaker.App;

internal sealed record RssCloudBackup(string Id, string FileName, long SizeBytes, string Sha256, DateTimeOffset CreatedAt);

internal sealed record RssAccountSession(string Id, string Email, string Name, string AppKey, string Plan, string LicenseStatus, string? ExpiresAt);

internal static class RssCoreAccountClient
{
    private const string ApiBase = "https://rsscore.cv/api/v1/customer-auth/";
    private const string ProjectKey = "rss-label-maker";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(25) };
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static string AccountDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Razeen Secure Solution", "RSS Label Maker", "account");
    private static string TokenPath => Path.Combine(AccountDirectory, "session.bin");

    public static async Task<string> RegisterAsync(string name, string email, string password)
    {
        using var response = await Http.PostAsJsonAsync(ApiBase + "register", new
        {
            display_name = name, email, password, project_key = ProjectKey
        });
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(ReadError(document.RootElement, "Registration failed."));
        var required = document.RootElement.TryGetProperty("email_verification_required", out var value) && value.GetBoolean();
        return required
            ? "Registration received. Open the verification email sent to your inbox, then return here and sign in. The link expires in 24 hours."
            : "Your RSS account is ready. You can now sign in.";
    }

    public static async Task<string> ResendVerificationAsync(string email)
    {
        using var response = await Http.PostAsJsonAsync(ApiBase + "resend-verification", new
        {
            email, project_key = ProjectKey
        });
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(ReadError(document.RootElement, "Unable to resend verification email."));
        return "If this account still needs verification, a new verification email has been sent.";
    }

    public static async Task<RssAccountSession> LoginAsync(string email, string password)
    {
        using var response = await Http.PostAsJsonAsync(ApiBase + "login", new
        {
            email, password, project_key = ProjectKey
        });
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(ReadError(document.RootElement, "Sign-in failed."));
        var root = document.RootElement;
        var token = root.GetProperty("token").GetString();
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("RSS Core did not return a session token.");
        SaveToken(token);
        return ReadSession(root);
    }

    public static async Task<RssAccountSession?> GetSessionAsync()
    {
        var token = LoadToken();
        if (string.IsNullOrWhiteSpace(token)) return null;
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiBase + "me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await Http.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            ClearToken();
            return null;
        }
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("RSS Core account service is temporarily unavailable.");
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return ReadSession(document.RootElement);
    }

    public static async Task SignOutAsync()
    {
        var token = LoadToken();
        try
        {
            if (!string.IsNullOrWhiteSpace(token))
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, ApiBase + "logout");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await Http.SendAsync(request);
            }
        }
        finally { ClearToken(); }
    }


    private const string BackupApi = "https://rsscore.cv/api/v1/customer-backups";
    private const string LicenseApi = "https://rsscore.cv/api/v1/customer-auth/license";

    public static async Task<IReadOnlyList<RssCloudBackup>> GetBackupsAsync()
    {
        using var response = await SendAuthenticatedAsync(HttpMethod.Get, BackupApi);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        EnsureSuccess(response, document.RootElement, "Unable to load cloud backups.");
        var items = new List<RssCloudBackup>();
        foreach (var item in document.RootElement.GetProperty("backups").EnumerateArray())
            items.Add(new RssCloudBackup(
                item.GetProperty("id").GetString() ?? "",
                item.GetProperty("file_name").GetString() ?? "backup.zip",
                item.GetProperty("size_bytes").GetInt64(),
                item.GetProperty("sha256").GetString() ?? "",
                DateTimeOffset.FromUnixTimeMilliseconds(item.GetProperty("created_at").GetInt64()).ToLocalTime()));
        return items;
    }

    public static async Task CreateBackupAsync(byte[] archive, string fileName, string sha256)
    {
        var payload = new
        {
            file_name = fileName,
            content_base64 = Convert.ToBase64String(archive),
            sha256
        };
        using var response = await SendAuthenticatedAsync(HttpMethod.Post, BackupApi, payload);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        EnsureSuccess(response, document.RootElement, "Cloud backup failed.");
    }

    public static async Task<byte[]> DownloadBackupAsync(string id)
    {
        using var response = await SendAuthenticatedAsync(HttpMethod.Get, BackupApi + "/" + Uri.EscapeDataString(id));
        if (!response.IsSuccessStatusCode)
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            throw new InvalidOperationException(ReadError(document.RootElement, "Unable to download backup."));
        }
        return await response.Content.ReadAsByteArrayAsync();
    }

    public static async Task<RssAccountSession> GetLicenseAsync()
    {
        using var response = await SendAuthenticatedAsync(HttpMethod.Get, LicenseApi);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        EnsureSuccess(response, document.RootElement, "Unable to load license information.");
        return ReadSession(document.RootElement);
    }

    private static async Task<HttpResponseMessage> SendAuthenticatedAsync(HttpMethod method, string url, object? body = null)
    {
        var token = LoadToken();
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Sign in to your RSS account to use cloud backup and license management.");
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        var response = await Http.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            ClearToken();
            throw new InvalidOperationException("Your RSS session has expired. Sign in again. Your local labels are unchanged.");
        }
        return response;
    }

    private static void EnsureSuccess(HttpResponseMessage response, JsonElement root, string fallback)
    {
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(ReadError(root, fallback));
    }

    private static RssAccountSession ReadSession(JsonElement root)
    {
        var user = root.GetProperty("user");
        var license = root.GetProperty("license");
        return new RssAccountSession(
            user.GetProperty("id").ToString(),
            user.GetProperty("email").GetString() ?? "",
            user.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
            license.TryGetProperty("app_key", out var key) ? key.GetString() ?? "" : "",
            license.TryGetProperty("plan", out var plan) ? plan.GetString() ?? "" : "",
            license.TryGetProperty("status", out var status) ? status.GetString() ?? "" : "",
            license.TryGetProperty("expires_at", out var expiry) && expiry.ValueKind != JsonValueKind.Null ? expiry.GetString() : null);
    }

    private static string ReadError(JsonElement root, string fallback) =>
        root.TryGetProperty("error", out var error) ? error.GetString() ?? fallback : fallback;

    private static void SaveToken(string token)
    {
        Directory.CreateDirectory(AccountDirectory);
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(TokenPath, protectedBytes);
    }

    private static string? LoadToken()
    {
        if (!File.Exists(TokenPath)) return null;
        try
        {
            var protectedBytes = File.ReadAllBytes(TokenPath);
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser));
        }
        catch { ClearToken(); return null; }
    }

    private static void ClearToken()
    {
        try { if (File.Exists(TokenPath)) File.Delete(TokenPath); } catch { }
    }
}
