using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace RSS.LabelMaker.App;

internal sealed record StagedUpdate(string Version, string PreviousVersion, string PackagePath, string ReleaseNotes);
internal sealed record UpdateWelcomeInfo(string PreviousVersion, string NewVersion, string ReleaseNotes, DateTimeOffset InstalledUtc);

internal static class UpdateService
{
    private const string ReleasesApi = "https://api.github.com/repos/riyazamra1/RSS-Label-Maker-/releases/latest";
    private const string PackageAsset = "RSS-Label-Maker-win-x64.zip";
    private const string ChecksumAsset = "RSS-Label-Maker-win-x64.zip.sha256";
    private const string ExecutableName = "RSS.LabelMaker.exe";

    public static async Task<StagedUpdate?> CheckAndStageAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("RSS-Label-Maker-Updater/1.1");
        client.Timeout = TimeSpan.FromMinutes(10);

        using var response = await client.GetAsync(ReleasesApi, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        using var release = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = release.RootElement;
        if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return null;
        if (root.TryGetProperty("prerelease", out var prerelease) && prerelease.GetBoolean()) return null;

        var releaseNotes = root.TryGetProperty("body", out var bodyElement) ? bodyElement.GetString() ?? "" : "";
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

        // Only accept assets hosted by the canonical GitHub release download host for this repository.
        static bool IsCanonicalReleaseAsset(string value) =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps &&
            uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.StartsWith("/riyazamra1/RSS-Label-Maker-/releases/download/", StringComparison.OrdinalIgnoreCase);
        if (!IsCanonicalReleaseAsset(packageUrl) || !IsCanonicalReleaseAsset(checksumUrl))
            throw new InvalidDataException("The release contains an unexpected update asset URL.");

        var rootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Razeen Secure Solution", "RSS Label Maker", "updates");
        Directory.CreateDirectory(rootPath);
        var packagePath = Path.Combine(rootPath, PackageAsset);
        var checksumPath = packagePath + ".sha256";
        var partialPackage = packagePath + ".download";
        var partialChecksum = checksumPath + ".download";

        try
        {
            await DownloadAsync(client, packageUrl, partialPackage, cancellationToken);
            await DownloadAsync(client, checksumUrl, partialChecksum, cancellationToken);
            var checksumText = await File.ReadAllTextAsync(partialChecksum, cancellationToken);
            var expectedHash = checksumText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Trim();
            if (string.IsNullOrWhiteSpace(expectedHash) || expectedHash.Length != 64 ||
                !expectedHash.All(Uri.IsHexDigit))
                throw new InvalidDataException("The release checksum file is invalid.");

            await using var packageStream = File.OpenRead(partialPackage);
            var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(packageStream, cancellationToken));
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(expectedHash), Convert.FromHexString(actualHash)))
                throw new InvalidDataException("The downloaded update failed SHA-256 integrity verification.");

            File.Move(partialPackage, packagePath, true);
            File.Move(partialChecksum, checksumPath, true);
            return new StagedUpdate(latest.ToString(), current.ToString(), packagePath, releaseNotes);
        }
        finally
        {
            TryDelete(partialPackage);
            TryDelete(partialChecksum);
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
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

    public static void MarkStartupHealthy()
    {
        var healthPath = Environment.GetEnvironmentVariable("RLM_UPDATE_HEALTH_PATH");
        if (string.IsNullOrWhiteSpace(healthPath)) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(healthPath)!);
            File.WriteAllText(healthPath, DateTimeOffset.UtcNow.ToString("O"));
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Update health signal failed: " + ex.Message); }
    }

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
        var updateRoot = Path.GetDirectoryName(update.PackagePath)!;
        var updaterPath = Path.Combine(updateRoot, "rlm-updater.ps1");
        var updateInfoPath = Path.Combine(updateRoot, "update-info.json");
        var healthPath = Path.Combine(updateRoot, $"update-health-{parentProcessId}.txt");
        TryDelete(healthPath);
        File.WriteAllText(updateInfoPath, JsonSerializer.Serialize(new UpdateWelcomeInfo(
            update.PreviousVersion, update.Version, update.ReleaseNotes, DateTimeOffset.UtcNow)));
        File.WriteAllText(updaterPath, UpdaterScript);

        var start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(updaterPath);
        start.ArgumentList.Add("-PackagePath");
        start.ArgumentList.Add(update.PackagePath);
        start.ArgumentList.Add("-ExecutablePath");
        start.ArgumentList.Add(executablePath);
        start.ArgumentList.Add("-ParentPid");
        start.ArgumentList.Add(parentProcessId.ToString());
        start.ArgumentList.Add("-UpdateInfoPath");
        start.ArgumentList.Add(updateInfoPath);
        start.ArgumentList.Add("-MarkerPath");
        start.ArgumentList.Add(GetUpdateMarkerPath());
        start.ArgumentList.Add("-HealthPath");
        start.ArgumentList.Add(healthPath);
        _ = Process.Start(start) ?? throw new InvalidOperationException("Could not start the separate update process.");
    }

    private const string UpdaterScript = """
param(
    [Parameter(Mandatory=$true)][string]$PackagePath,
    [Parameter(Mandatory=$true)][string]$ExecutablePath,
    [Parameter(Mandatory=$true)][int]$ParentPid,
    [Parameter(Mandatory=$true)][string]$UpdateInfoPath,
    [Parameter(Mandatory=$true)][string]$MarkerPath,
    [Parameter(Mandatory=$true)][string]$HealthPath
)
$ErrorActionPreference = 'Stop'
$installDir = Split-Path -Parent $ExecutablePath
$exeName = Split-Path -Leaf $ExecutablePath
$parentDir = Split-Path -Parent $installDir
$installLeaf = Split-Path -Leaf $installDir
$workRoot = Join-Path $env:LOCALAPPDATA 'Razeen Secure SolutionRSS Label Maker'
$stageDir = Join-Path $workRoot 'update-stage'
$candidateDir = Join-Path $parentDir ($installLeaf + '.rlm-new')
$backupDir = Join-Path $parentDir ($installLeaf + '.rlm-previous')
$packageDir = Join-Path $stageDir 'package'
$swapped = $false
$newProcess = $null

function Remove-Safe([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

try {
    $parentDeadline = (Get-Date).AddSeconds(180)
    while (Get-Process -Id $ParentPid -ErrorAction SilentlyContinue) {
        if ((Get-Date) -gt $parentDeadline) { throw 'The previous application did not close in time; update was cancelled.' }
        Start-Sleep -Seconds 1
    }

    Remove-Safe $stageDir
    Remove-Safe $candidateDir
    New-Item -ItemType Directory -Force -Path $packageDir | Out-Null
    Expand-Archive -LiteralPath $PackagePath -DestinationPath $packageDir -Force

    $stagedExe = Join-Path $packageDir $exeName
    $stagedAssembly = Join-Path $packageDir 'RSS.LabelMaker.dll'
    if (!(Test-Path -LiteralPath $stagedExe) -or !(Test-Path -LiteralPath $stagedAssembly)) {
        throw 'The update archive is incomplete; the application executable or managed assembly is missing.'
    }

    # Copy the complete self-contained publish output beside the installation. User data lives in LocalAppData and is not touched.
    New-Item -ItemType Directory -Force -Path $candidateDir | Out-Null
    Copy-Item -Path (Join-Path $packageDir '*') -Destination $candidateDir -Recurse -Force
    if (!(Test-Path -LiteralPath (Join-Path $candidateDir $exeName)) -or
        !(Test-Path -LiteralPath (Join-Path $candidateDir 'RSS.LabelMaker.dll'))) {
        throw 'The staged application failed its file validation.'
    }

    Remove-Safe $backupDir
    if (Test-Path -LiteralPath $installDir) {
        Move-Item -LiteralPath $installDir -Destination $backupDir
    }
    try {
        Move-Item -LiteralPath $candidateDir -Destination $installDir
        $swapped = $true
    } catch {
        if (Test-Path -LiteralPath $backupDir) {
            Move-Item -LiteralPath $backupDir -Destination $installDir
        }
        throw
    }

    $markerDir = Split-Path -Parent $MarkerPath
    New-Item -ItemType Directory -Force -Path $markerDir | Out-Null
    Copy-Item -LiteralPath $UpdateInfoPath -Destination $MarkerPath -Force
    if (Test-Path -LiteralPath $HealthPath) { Remove-Item -LiteralPath $HealthPath -Force }
    $env:RLM_UPDATE_HEALTH_PATH = $HealthPath
    $newProcess = Start-Process -FilePath $ExecutablePath -PassThru

    $healthDeadline = (Get-Date).AddSeconds(180)
    while (!(Test-Path -LiteralPath $HealthPath)) {
        $newProcess.Refresh()
        if ($newProcess.HasExited) { throw 'Updated application exited before confirming successful RSS account startup; rolling back.' }
        if ((Get-Date) -gt $healthDeadline) { throw 'Updated application did not complete startup validation; rolling back.' }
        Start-Sleep -Seconds 1
    }
    $newProcess.Refresh()
    if ($newProcess.HasExited) { throw 'Updated application exited immediately after startup validation; rolling back.' }

    Remove-Item Env:RLM_UPDATE_HEALTH_PATH -ErrorAction SilentlyContinue
    Remove-Safe $backupDir
    Remove-Safe $stageDir
    if (Test-Path -LiteralPath $HealthPath) { Remove-Item -LiteralPath $HealthPath -Force }
} catch {
    Remove-Item Env:RLM_UPDATE_HEALTH_PATH -ErrorAction SilentlyContinue
    if ($newProcess -and !$newProcess.HasExited) {
        Stop-Process -Id $newProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $MarkerPath) {
        Remove-Item -LiteralPath $MarkerPath -Force -ErrorAction SilentlyContinue
    }
    if ($swapped -and (Test-Path -LiteralPath $installDir)) {
        Remove-Safe $installDir
    }
    if (Test-Path -LiteralPath $backupDir) {
        Move-Item -LiteralPath $backupDir -Destination $installDir -Force
    }
    if (Test-Path -LiteralPath $ExecutablePath) {
        Start-Process -FilePath $ExecutablePath -ErrorAction SilentlyContinue
    }
    exit 1
}
""";
}
