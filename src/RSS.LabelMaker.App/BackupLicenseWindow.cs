using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.Sqlite;

namespace RSS.LabelMaker.App;

internal sealed class BackupLicenseWindow : Window
{
    private readonly ListBox _backups = new() { MinHeight = 220, BorderBrush = Brush("#E1E4E8"), BorderThickness = new Thickness(1) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brush("#66717D"), Margin = new Thickness(0, 12, 0, 0) };
    private readonly TextBlock _license = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brush("#303844") };
    private readonly Button _connect = new() { Content = "Connect Google Drive", Height = 38, Background = Brush("#F0F1F3"), Foreground = Brush("#20242B") };
    private readonly Button _create = new() { Content = "Create Google Drive backup", Height = 40, Background = Brush("#C69A43"), Foreground = Brush("#20242B"), FontWeight = FontWeights.SemiBold };
    private readonly Button _restore = new() { Content = "Restore selected backup", Height = 38, Background = Brush("#20242B"), Foreground = Brushes.White };
    private readonly Button _refresh = new() { Content = "Refresh list", Height = 38, Background = Brush("#F0F1F3"), Foreground = Brush("#20242B") };
    private IReadOnlyList<GoogleDriveBackup> _items = Array.Empty<GoogleDriveBackup>();

    private static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Razeen Secure Solution", "RSS Label Maker");
    private static string DatabasePath => Path.Combine(DataDirectory, "rss-label-maker.db");

    public BackupLicenseWindow()
    {
        Title = "RSS Label Maker — Google Drive Backup & License";
        Width = 700; Height = 730; MinWidth = 640; MinHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#F5F6F8");
        Build();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private void Build()
    {
        var root = new StackPanel { Margin = new Thickness(26) };
        root.Children.Add(new TextBlock { Text = "RSS CLOUD SERVICES", Foreground = Brush("#A47A32"), FontSize = 11, FontWeight = FontWeights.Bold });
        root.Children.Add(new TextBlock { Text = "Google Drive Backup & Restore", Foreground = Brush("#20242B"), FontSize = 25, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 5, 0, 4) });
        root.Children.Add(new TextBlock { Text = "Your product database stays on this PC. Cloud backups are saved only to your Google Drive in the “RSS Label Maker Backups” folder. Normal label creation and printing work offline.", Foreground = Brush("#68717D"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 18) });

        var licenseCard = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(8), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 16) };
        var licenseStack = new StackPanel();
        licenseStack.Children.Add(new TextBlock { Text = "RSS CORE ACCOUNT & LICENSE", Foreground = Brush("#98702B"), FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
        licenseStack.Children.Add(_license);
        licenseStack.Children.Add(new TextBlock { Text = "Account details and license information are retrieved from RSS Core. They are not included in Google Drive backup files.", Foreground = Brush("#89919B"), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });
        licenseCard.Child = licenseStack;
        root.Children.Add(licenseCard);

        root.Children.Add(new TextBlock { Text = "GOOGLE DRIVE BACKUPS", Foreground = Brush("#77808C"), FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
        root.Children.Add(_backups);
        var actions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        _connect.Margin = new Thickness(0, 0, 8, 8); _create.Margin = new Thickness(0, 0, 8, 8);
        _restore.Margin = new Thickness(0, 0, 8, 8); _refresh.Margin = new Thickness(0, 0, 8, 8);
        actions.Children.Add(_connect); actions.Children.Add(_create); actions.Children.Add(_restore); actions.Children.Add(_refresh);
        root.Children.Add(actions);
        root.Children.Add(_status);
        root.Children.Add(new TextBlock { Text = "Restore checks the backup checksum and SQLite integrity, then creates a consistent local safety copy before replacing the product database. Application settings and RSS account details are not part of the backup.", Foreground = Brush("#89919B"), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 0) });
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _connect.Click += async (_, _) => await ConnectAsync();
        _create.Click += async (_, _) => await CreateAsync();
        _refresh.Click += async (_, _) => await RefreshAsync();
        _restore.Click += async (_, _) => await RestoreAsync();
    }

    private async Task ConnectAsync()
    {
        SetBusy(true, "Opening Google sign-in…");
        try
        {
            await GoogleDriveBackupClient.ConnectAsync();
            _status.Text = "Google Drive connected. Your authorization is stored locally on this PC.";
            await RefreshAsync();
        }
        catch (Exception ex) { _status.Text = ex.Message; }
        finally { SetBusy(false); }
    }

    private async Task RefreshAsync()
    {
        SetBusy(true, "Loading RSS Core license and Google Drive backup list…");
        try
        {
            var license = await RssCoreAccountClient.GetLicenseAsync();
            _license.Text = $"{license.Plan} · {license.LicenseStatus}\nAccount: {license.Email}\nLicense key: {license.AppKey}\nExpiry: {license.ExpiresAt ?? "Not provided"}";
            if (!GoogleDriveBackupClient.IsConfigured)
            {
                _items = Array.Empty<GoogleDriveBackup>();
                _backups.ItemsSource = Array.Empty<string>();
                _status.Text = GoogleDriveBackupClient.SetupInstructions;
                return;
            }
            if (!GoogleDriveBackupClient.HasSavedAuthorization)
            {
                _items = Array.Empty<GoogleDriveBackup>();
                _backups.ItemsSource = Array.Empty<string>();
                _status.Text = "Google Drive is configured but not connected. Select Connect Google Drive to sign in.";
                return;
            }
            _items = await GoogleDriveBackupClient.GetBackupsAsync();
            _backups.ItemsSource = _items.Select(x => $"{x.CreatedAt:yyyy-MM-dd HH:mm}  ·  {x.FileName}  ·  {FormatSize(x.SizeBytes)}").ToArray();
            _status.Text = _items.Count == 0 ? "No backups found in the RSS Label Maker Backups folder. Create your first backup." : $"{_items.Count} Google Drive backup(s) available.";
        }
        catch (Exception ex) { _status.Text = ex.Message; }
        finally { SetBusy(false); }
    }

    private async Task CreateAsync()
    {
        SetBusy(true, "Creating a consistent SQLite snapshot…");
        string? snapshotPath = null;
        try
        {
            if (!File.Exists(DatabasePath)) throw new FileNotFoundException("The local product database was not found.");
            if (!GoogleDriveBackupClient.IsConfigured) throw new InvalidOperationException(GoogleDriveBackupClient.SetupInstructions);
            Directory.CreateDirectory(DataDirectory);
            snapshotPath = Path.Combine(DataDirectory, "backup-snapshot-" + Guid.NewGuid().ToString("N") + ".db");
            await CreateSqliteSnapshotAsync(DatabasePath, snapshotPath);

            var archiveBytes = await Task.Run(() =>
            {
                using var output = new MemoryStream();
                using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
                {
                    var dbEntry = archive.CreateEntry("rss-label-maker.db", CompressionLevel.Optimal);
                    using (var target = dbEntry.Open())
                    using (var source = File.OpenRead(snapshotPath)) source.CopyTo(target);
                    var manifest = archive.CreateEntry("backup-manifest.json", CompressionLevel.Optimal);
                    using var writer = new StreamWriter(manifest.Open(), Encoding.UTF8);
                    writer.Write(JsonSerializer.Serialize(new { app = "RSS Label Maker", format = 1, created_utc = DateTimeOffset.UtcNow }));
                }
                return output.ToArray();
            });
            if (archiveBytes.Length > 100 * 1024 * 1024) throw new InvalidOperationException("This backup exceeds the current 100 MB per-backup safety limit.");
            var hash = Convert.ToHexString(SHA256.HashData(archiveBytes)).ToLowerInvariant();
            var fileName = $"rss-label-maker-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
            await GoogleDriveBackupClient.UploadBackupAsync(archiveBytes, fileName, hash);
            _status.Text = "Backup uploaded to your Google Drive and checksum recorded.";
            await RefreshAsync();
        }
        catch (Exception ex) { _status.Text = ex.Message; }
        finally
        {
            if (snapshotPath is not null) try { File.Delete(snapshotPath); } catch { }
            SetBusy(false);
        }
    }

    private async Task RestoreAsync()
    {
        var index = _backups.SelectedIndex;
        if (index < 0 || index >= _items.Count) { _status.Text = "Select a Google Drive backup first."; return; }
        var selected = _items[index];
        if (string.IsNullOrWhiteSpace(selected.Sha256)) { _status.Text = "This backup has no checksum metadata and cannot be safely restored."; return; }
        var answer = MessageBox.Show(this,
            $"Restore {selected.FileName}?\n\nThe current local database will first be copied to a safety file. Products saved after this backup will not be in the restored database.",
            "Confirm database restore", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        SetBusy(true, "Downloading and validating the selected Google Drive backup…");
        string? temporaryDb = null;
        try
        {
            var archiveBytes = await GoogleDriveBackupClient.DownloadBackupAsync(selected.Id);
            var hash = Convert.ToHexString(SHA256.HashData(archiveBytes)).ToLowerInvariant();
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(hash), Encoding.ASCII.GetBytes(selected.Sha256.ToLowerInvariant())))
                throw new InvalidDataException("Backup checksum validation failed. The local database was not changed.");

            Directory.CreateDirectory(DataDirectory);
            temporaryDb = Path.Combine(DataDirectory, "restore-" + Guid.NewGuid().ToString("N") + ".db");
            using (var input = new MemoryStream(archiveBytes))
            using (var archive = new ZipArchive(input, ZipArchiveMode.Read))
            {
                var entry = archive.GetEntry("rss-label-maker.db") ?? throw new InvalidDataException("Backup archive does not contain the product database.");
                if (entry.Length == 0 || entry.Length > 512 * 1024 * 1024) throw new InvalidDataException("Backup database size is invalid.");
                using var source = entry.Open();
                using var target = File.Create(temporaryDb);
                await source.CopyToAsync(target);
            }

            await ValidateDatabaseAsync(temporaryDb);
            var safetyCopy = Path.Combine(DataDirectory, "rss-label-maker-before-restore-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".db");
            if (File.Exists(DatabasePath))
                await CreateSqliteSnapshotAsync(DatabasePath, safetyCopy);

            TryDelete(DatabasePath + "-wal");
            TryDelete(DatabasePath + "-shm");
            if (File.Exists(DatabasePath)) File.Replace(temporaryDb, DatabasePath, null);
            else File.Move(temporaryDb, DatabasePath);
            temporaryDb = null;
            _status.Text = $"Restore completed. Previous database safety copy: {Path.GetFileName(safetyCopy)}";
            MessageBox.Show(this, "Product database restored. RSS Label Maker will refresh the product list when this window closes.", "Restore complete", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex) { _status.Text = "Restore failed. Existing local data was not intentionally replaced. " + ex.Message; }
        finally
        {
            if (temporaryDb is not null) TryDelete(temporaryDb);
            SetBusy(false);
        }
    }

    private static async Task CreateSqliteSnapshotAsync(string sourcePath, string destinationPath)
    {
        await Task.Run(() =>
        {
            using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = sourcePath, Mode = SqliteOpenMode.ReadOnly }.ToString());
            using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destinationPath }.ToString());
            source.Open();
            destination.Open();
            source.BackupDatabase(destination);
        });
    }

    private static async Task ValidateDatabaseAsync(string databasePath)
    {
        await Task.Run(() =>
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            if (!string.Equals(command.ExecuteScalar()?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The backup database failed SQLite integrity validation.");
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Products' LIMIT 1;";
            if (command.ExecuteScalar() is null) throw new InvalidDataException("The backup does not contain the RSS Label Maker Products table.");
        });
    }

    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    private void SetBusy(bool busy, string? message = null)
    {
        _connect.IsEnabled = !busy; _create.IsEnabled = !busy; _restore.IsEnabled = !busy; _refresh.IsEnabled = !busy;
        if (message is not null) _status.Text = message;
    }
    private static string FormatSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024d:0.0} MB" : $"{bytes / 1024d:0.0} KB";
    private static SolidColorBrush Brush(string value) => new((Color)ColorConverter.ConvertFromString(value));
}
