using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RSS.LabelMaker.App;

internal sealed record GoogleDriveBackup(string Id, string FileName, long SizeBytes, string Sha256, DateTimeOffset CreatedAt);

internal static class GoogleDriveBackupClient
{
    private const string FolderName = "RSS Label Maker Backups";
    private static readonly string AppDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Razeen Secure Solution", "RSS Label Maker");
    private static readonly string OAuthConfigPath = Path.Combine(AppDirectory, "google-drive-oauth.json");
    private static readonly string TokenDirectory = Path.Combine(AppDirectory, "google-drive-token");
    private static readonly string[] Scopes = { DriveService.Scope.DriveFile };

    public static bool IsConfigured => System.IO.File.Exists(OAuthConfigPath);
    public static bool HasSavedAuthorization => Directory.Exists(TokenDirectory) && Directory.GetFiles(TokenDirectory, "*.bin").Length > 0;

    public static string SetupInstructions =>
        "Google Drive needs a Google OAuth Desktop app configuration. Save the downloaded OAuth client JSON as:\n" +
        OAuthConfigPath + "\n" +
        "The configuration and encrypted Google sign-in token stay on this Windows PC. RSS Core does not receive them.";

    public static async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _ = await AuthorizeAsync(cancellationToken);
    }

    public static async Task<IReadOnlyList<GoogleDriveBackup>> GetBackupsAsync(CancellationToken cancellationToken = default)
    {
        var service = await CreateServiceAsync(cancellationToken);
        var folderId = await GetOrCreateFolderAsync(service, cancellationToken);
        var request = service.Files.List();
        request.Q = "'" + folderId + "' in parents and trashed = false";
        request.PageSize = 100;
        request.OrderBy = "createdTime desc";
        request.Fields = "files(id,name,size,createdTime,appProperties),nextPageToken";
        var response = await request.ExecuteAsync(cancellationToken);
        return (response.Files ?? new List<Google.Apis.Drive.v3.Data.File>())
            .Where(file => file.Id is not null)
            .Select(file => new GoogleDriveBackup(
                file.Id!,
                file.Name ?? "RSS Label Maker backup.zip",
                file.Size ?? 0,
                file.AppProperties is not null && file.AppProperties.TryGetValue("sha256", out var hash) ? hash : "",
                file.CreatedTime.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(file.CreatedTime.Value, DateTimeKind.Utc)).ToLocalTime() : DateTimeOffset.MinValue))
            .ToArray();
    }

    public static async Task UploadBackupAsync(byte[] archiveBytes, string fileName, string sha256, CancellationToken cancellationToken = default)
    {
        var service = await CreateServiceAsync(cancellationToken);
        var folderId = await GetOrCreateFolderAsync(service, cancellationToken);
        var metadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = fileName,
            MimeType = "application/zip",
            Parents = new[] { folderId },
            AppProperties = new Dictionary<string, string> { ["sha256"] = sha256, ["format"] = "1" }
        };
        using var stream = new MemoryStream(archiveBytes, writable: false);
        var request = service.Files.Create(metadata, stream, "application/zip");
        request.Fields = "id,name,size,createdTime,appProperties";
        var result = await request.UploadAsync(cancellationToken);
        if (result.Status != Google.Apis.Upload.UploadStatus.Completed)
            throw result.Exception ?? new InvalidOperationException("Google Drive did not complete the backup upload.");
    }

    public static async Task<byte[]> DownloadBackupAsync(string fileId, CancellationToken cancellationToken = default)
    {
        var service = await CreateServiceAsync(cancellationToken);
        using var output = new MemoryStream();
        var result = await service.Files.Get(fileId).DownloadAsync(output, cancellationToken);
        if (result.Status != Google.Apis.Download.DownloadStatus.Completed)
            throw result.Exception ?? new InvalidOperationException("Google Drive did not complete the backup download.");
        return output.ToArray();
    }

    private static async Task<DriveService> CreateServiceAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new InvalidOperationException(SetupInstructions);
        var credential = await AuthorizeAsync(cancellationToken);
        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "RSS Label Maker"
        });
    }

    private static async Task<UserCredential> AuthorizeAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new InvalidOperationException(SetupInstructions);
        Directory.CreateDirectory(AppDirectory);
        var secrets = GoogleClientSecrets.FromFile(OAuthConfigPath).Secrets;
        if (string.IsNullOrWhiteSpace(secrets.ClientId) || string.IsNullOrWhiteSpace(secrets.ClientSecret))
            throw new InvalidOperationException("The Google OAuth JSON is invalid. Download an OAuth client configuration for a Desktop app.");
        return await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets, Scopes, "rss-label-maker", cancellationToken, new DpapiDataStore(TokenDirectory));
    }

    private static async Task<string> GetOrCreateFolderAsync(DriveService service, CancellationToken cancellationToken)
    {
        var escapedName = FolderName.Replace("'", "\\'");
        var list = service.Files.List();
        list.Q = "mimeType = 'application/vnd.google-apps.folder' and name = '" + escapedName + "' and trashed = false";
        list.PageSize = 10;
        list.Fields = "files(id,name)";
        var existing = await list.ExecuteAsync(cancellationToken);
        var folder = existing.Files?.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(folder?.Id)) return folder.Id;

        var metadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = FolderName,
            MimeType = "application/vnd.google-apps.folder"
        };
        var created = await service.Files.Create(metadata).ExecuteAsync(cancellationToken);
        return created.Id ?? throw new InvalidOperationException("Google Drive did not return the backup folder ID.");
    }

    private sealed class DpapiDataStore : IDataStore
    {
        private readonly string _directory;
        public DpapiDataStore(string directory) => _directory = directory;

        public async Task StoreAsync<T>(string key, T value)
        {
            Directory.CreateDirectory(_directory);
            var path = GetPath(key);
            var clear = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value));
            var encrypted = ProtectedData.Protect(clear, null, DataProtectionScope.CurrentUser);
            await System.IO.File.WriteAllBytesAsync(path, encrypted);
            CryptographicOperations.ZeroMemory(clear);
        }

        public async Task<T> GetAsync<T>(string key)
        {
            var path = GetPath(key);
            if (!System.IO.File.Exists(path)) return default!;
            var encrypted = await System.IO.File.ReadAllBytesAsync(path);
            var clear = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            try { return JsonSerializer.Deserialize<T>(clear)!; }
            finally { CryptographicOperations.ZeroMemory(clear); }
        }

        public Task DeleteAsync<T>(string key)
        {
            var path = GetPath(key);
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            if (Directory.Exists(_directory))
                foreach (var file in Directory.GetFiles(_directory)) System.IO.File.Delete(file);
            return Task.CompletedTask;
        }

        private string GetPath(string key)
        {
            var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
            return Path.Combine(_directory, name + ".bin");
        }
    }
}
