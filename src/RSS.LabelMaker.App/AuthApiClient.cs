using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace RSS.LabelMaker.App;

internal sealed class AuthApiClient
{
    // Production endpoint must be deployed and configured before release.
    public const string BaseUrl = "https://auth.rssapps.net";
    private static readonly HttpClient Http = new() { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(12) };
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<AuthResult> PostAsync(string path, object payload, string? token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(payload) };
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await Http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        AuthResult? result = null;
        try { result = JsonSerializer.Deserialize<AuthResult>(text, JsonOptions); } catch { }
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(result?.Error ?? $"Authentication request failed (HTTP {(int)response.StatusCode}).");
        return result ?? new AuthResult();
    }

    public Task<AuthResult> RegisterAsync(string name,string email,string password) =>
        PostAsync("/auth/register",new { fullName=name,email,password });
    public Task<AuthResult> VerifyAsync(string email,string code) =>
        PostAsync("/auth/verify",new { email,code,deviceName=Environment.MachineName });
    public Task<AuthResult> LoginAsync(string email,string password) =>
        PostAsync("/auth/login",new { email,password,deviceName=Environment.MachineName });
    public Task<AuthResult> ResendAsync(string email) =>
        PostAsync("/auth/resend-verification",new { email });
    public Task<AuthResult> ForgotAsync(string email) =>
        PostAsync("/auth/forgot-password",new { email });
    public Task<AuthResult> ResetAsync(string email,string code,string password) =>
        PostAsync("/auth/reset-password",new { email,code,password });

    public async Task<bool> ValidateAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/session");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await Http.SendAsync(request);
        if (response.IsSuccessStatusCode) return true;
        if ((int)response.StatusCode is 401 or 403) return false;
        throw new HttpRequestException("Authentication service is temporarily unreachable.");
    }
}

internal sealed class AuthResult
{
    public string? Token { get; set; }
    public string? Error { get; set; }
    public string? Message { get; set; }
}