using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RSS.LabelMaker.App;

internal sealed record StagedUpdate(string Version, string PreviousVersion, string PackagePath, string ReleaseNotes);
internal sealed record UpdateWelcomeInfo(string PreviousVersion, string NewVersion, string ReleaseNotes, DateTimeOffset InstalledUtc);

internal static class UpdateService
{
    private const string ReleasesApi = "https://api.github.com/repos/riyazamra1/RSS-Label-Maker-/releases/latest";
    private const string PackageAsset = "RSS-Label-Maker-win-x64.zip";
    private const string ChecksumAsset = "RSS-Label-Maker-win-x64.zip.sha256";

    public static async Task<StagedUpdate?> CheckAndStageAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("RSS-Label-Maker-Updater/1.0");
        client.Timeout = TimeSpan.FromMinutes(5);

        using var response = await client.GetAsync(ReleasesApi, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        using var release = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = release.RootElement;
        var releaseNotes = root.TryGetProperty("body", out var bodyElement) ? bodyElement.GetString() ?? "" : "";
        // Never surface repository/provider URLs or provider branding inside the application.
        releaseNotes = Regex.Replace(releaseNotes, @"(?im)^.*(?:github\\.com|githubusercontent\\.com).*$", "").Trim();
        releaseNotes = Regex.Replace(releaseNotes, @"https?://\\S+", "[external link omitted]");
        var tag = root.GetProperty("tag_name").GetString()?.TrimStart('v', 'V');
        if (!Version.TryParse(tag, out var latest)) return null;

        latest = new Version(latest.Major, latest.Minor, Math.Max(0, latest.Build), Math.Max(0, latest.Revision));
        var current = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0, 0);
        if (latest <= current) return null;

        var assets = root.GetProperty("assets").EnumerateArray()
            .ToDictionary(x => x.GetProperty("name").GetString() ?? "",
                          x => x.GetProperty("browser_download_url").GetString() ?? "",
                          StringComparer.OrdinalIgnoreCase);
        if (!assets.TryGetValue(PackageAsset, out var packageUrl) ||
            !assets.TryGetValue(ChecksumAsset, out var checksumUrl))
            throw new InvalidOperationException("The latest release is missing the Windows package or its SHA-256 checksum.");

        var rootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Razeen Secure Solution", "RSS Label Maker", "updates");
        Directory.CreateDirectory(rootPath);
        var packagePath = Path.Combine(rootPath, PackageAsset);
        var checksumPath = packagePath + ".sha256";

        await DownloadAsync(client, packageUrl, packagePath, cancellationToken);
        await DownloadAsync(client, checksumUrl, checksumPath, cancellationToken);

        var expectedHash = (await File.ReadAllTextAsync(checksumPath, cancellationToken))
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(expectedHash) || expectedHash.Length != 64)
            throw new InvalidDataException("The release checksum file is invalid.");

        await using var packageStream = File.OpenRead(packagePath);
        var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(packageStream, cancellationToken));
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expectedHash), Convert.FromHexString(actualHash)))
            throw new InvalidDataException("The downloaded update failed SHA-256 integrity verification.");

        return new StagedUpdate(latest.ToString(), current.ToString(), packagePath, releaseNotes);
    }

    private static async Task DownloadAsync(HttpClient client, string url, string path, CancellationToken token)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await input.CopyToAsync(output, token);
        await output.FlushAsync(token);
    }

    public static string GetUpdateMarkerPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Razeen Secure Solution", "RSS Label Maker", "update-completed.json");

    public static UpdateWelcomeInfo? ReadPendingUpdate()
    {
        var path = GetUpdateMarkerPath();
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<UpdateWelcomeInfo>(File.ReadAllText(path)); }
        catch { return null; }
    }

    public static void AcknowledgeUpdate() 
    {
        var path = GetUpdateMarkerPath();
        if (File.Exists(path)) File.Delete(path);
    }

    public static void StartUpdater(StagedUpdate update, string executablePath, int parentProcessId)
    {
        var packagePath = update.PackagePath;
        var updaterPath = Path.Combine(Path.GetDirectoryName(packagePath)!, "rlm-updater.ps1");
        var updateInfoPath = Path.Combine(Path.GetDirectoryName(packagePath)!, "update-info.json");
        File.WriteAllText(updateInfoPath, JsonSerializer.Serialize(new UpdateWelcomeInfo(
            update.PreviousVersion, update.Version, update.ReleaseNotes, DateTimeOffset.UtcNow)));
        File.WriteAllText(updaterPath, UpdaterScript);
        var start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(updaterPath);
        start.ArgumentList.Add("-PackagePath");
        start.ArgumentList.Add(packagePath);
        start.ArgumentList.Add("-ExecutablePath");
        start.ArgumentList.Add(executablePath);
        start.ArgumentList.Add("-ParentPid");
        start.ArgumentList.Add(parentProcessId.ToString());
        start.ArgumentList.Add("-UpdateInfoPath");
        start.ArgumentList.Add(updateInfoPath);
        start.ArgumentList.Add("-MarkerPath");
        start.ArgumentList.Add(GetUpdateMarkerPath());
        _ = Process.Start(start) ?? throw new InvalidOperationException("Could not start the separate update process.");
    }

    private const string UpdaterScript = """
param(
    [Parameter(Mandatory=$true)][string]$PackagePath,
    [Parameter(Mandatory=$true)][string]$ExecutablePath,
    [Parameter(Mandatory=$true)][int]$ParentPid,
    [Parameter(Mandatory=$true)][string]$UpdateInfoPath,
    [Parameter(Mandatory=$true)][string]$MarkerPath
)
$ErrorActionPreference = 'Stop'
$installDir = Split-Path -Parent $ExecutablePath
$exeName = Split-Path -Leaf $ExecutablePath
$workDir = Join-Path $env:LOCALAPPDATA 'Razeen Secure Solution\RSS Label Maker\update-stage'
$backupPath = Join-Path $workDir ($exeName + '.previous')
New-Item -ItemType Directory -Force -Path $workDir | Out-Null
try {
    Wait-Process -Id $ParentPid -Timeout 120 -ErrorAction Stop
} catch {
    # Never replace an executable while its original process may still be running.
    exit 2
}
if (Get-Process -Id $ParentPid -ErrorAction SilentlyContinue) {
    exit 2
}
try {
    if (Test-Path $backupPath) { Remove-Item $backupPath -Force }
    Expand-Archive -LiteralPath $PackagePath -DestinationPath $workDir -Force
    $stagedExe = Join-Path $workDir $exeName
    if (!(Test-Path $stagedExe)) { throw 'Update archive does not contain the expected executable.' }
    if (Test-Path $ExecutablePath) { Copy-Item $ExecutablePath $backupPath -Force }
    Copy-Item $stagedExe $ExecutablePath -Force
    $markerDir = Split-Path -Parent $MarkerPath
    New-Item -ItemType Directory -Force -Path $markerDir | Out-Null
    Copy-Item $UpdateInfoPath $MarkerPath -Force
    $newProcess = Start-Process -FilePath $ExecutablePath -PassThru
    Start-Sleep -Seconds 8
    $newProcess.Refresh()
    if ($newProcess.HasExited) { throw 'Updated application exited during startup; rolling back.' }
    if (Test-Path $backupPath) { Remove-Item $backupPath -Force }
    Remove-Item $workDir -Recurse -Force -ErrorAction SilentlyContinue
} catch {
    if (Test-Path $MarkerPath) { Remove-Item $MarkerPath -Force -ErrorAction SilentlyContinue }
    if (Test-Path $backupPath) { Copy-Item $backupPath $ExecutablePath -Force }
    Start-Process -FilePath $ExecutablePath -ErrorAction SilentlyContinue
    exit 1
}
""";
}
