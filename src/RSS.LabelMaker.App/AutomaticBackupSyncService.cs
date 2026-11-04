using System.IO;

namespace RSS.LabelMaker.App;

internal static class AutomaticBackupSyncService
{
    private static readonly object Gate = new();
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(5);
    private static string? _databasePath;
    private static string? _pendingPath;
    private static CancellationTokenSource? _cancellation;
    private static Task? _worker;
    private static Action<string>? _statusChanged;
    private static DateTimeOffset _lastChangeUtc;
    private static long _changeVersion;
    private static bool _pending;
    private static string _status = "Automatic backup sync is idle.";

    public static string CurrentStatus { get { lock (Gate) return _status; } }

    public static void Start(string databasePath, Action<string>? statusChanged = null)
    {
        CancellationToken token;
        lock (Gate)
        {
            _databasePath = databasePath;
            _pendingPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Razeen Secure Solution", "RSS Label Maker", "automatic-backup.pending");
            _statusChanged = statusChanged;
            if (File.Exists(_pendingPath))
            {
                _pending = true;
                if (DateTimeOffset.TryParse(File.ReadAllText(_pendingPath), out var saved))
                    _lastChangeUtc = saved;
                else
                    _lastChangeUtc = DateTimeOffset.UtcNow;
            }
            else if (GoogleDriveBackupClient.IsConfigured && GoogleDriveBackupClient.HasSavedAuthorization)
            {
                _pending = true;
                _lastChangeUtc = DateTimeOffset.UtcNow;
                PersistPendingLocked();
            }

            if (_cancellation is null)
                _cancellation = new CancellationTokenSource();
            token = _cancellation.Token;
            if (_worker is null || _worker.IsCompleted)
                _worker = Task.Run(() => RunAsync(token));
        }
        Publish("Automatic backup sync is ready.");
    }

    public static void MarkDatabaseChanged() => RequestSync();

    public static void RequestSync()
    {
        lock (Gate)
        {
            if (_databasePath is null) return;
            _pending = true;
            _changeVersion++;
            _lastChangeUtc = DateTimeOffset.UtcNow;
            PersistPendingLocked();
        }
        Publish("Database change queued. Automatic Google Drive backup will run in the background.");
    }

    public static void Stop()
    {
        lock (Gate)
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            _worker = null;
        }
    }

    private static async Task RunAsync(CancellationToken cancellationToken)
    {
        var nextRetryUtc = DateTimeOffset.MinValue;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                string? databasePath;
                long version;
                DateTimeOffset changedUtc;
                lock (Gate)
                {
                    if (!_pending) continue;
                    databasePath = _databasePath;
                    version = _changeVersion;
                    changedUtc = _lastChangeUtc;
                }

                if (databasePath is null) continue;
                if (!GoogleDriveBackupClient.IsConfigured || !GoogleDriveBackupClient.HasSavedAuthorization)
                {
                    Publish("Automatic backup is waiting for Google Drive setup/sign-in. Local data remains saved on this PC.");
                    continue;
                }
                if (DateTimeOffset.UtcNow - changedUtc < Debounce || DateTimeOffset.UtcNow < nextRetryUtc)
                    continue;

                Publish("Creating and uploading an automatic database backup…");
                await BackupLicenseWindow.UploadDatabaseSnapshotAsync(databasePath, cancellationToken);

                bool moreChanges;
                lock (Gate)
                {
                    moreChanges = _changeVersion != version;
                    if (!moreChanges)
                    {
                        _pending = false;
                        if (_pendingPath is not null && File.Exists(_pendingPath)) File.Delete(_pendingPath);
                    }
                }
                nextRetryUtc = DateTimeOffset.MinValue;
                Publish(moreChanges
                    ? "A newer database change is queued for the next automatic backup."
                    : $"Automatic Google Drive backup completed at {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                nextRetryUtc = DateTimeOffset.UtcNow.Add(RetryInterval);
                Publish($"Automatic backup failed; local data is safe. Will retry in 5 minutes. {ex.Message}");
            }
        }
    }

    private static void PersistPendingLocked()
    {
        if (_pendingPath is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_pendingPath)!);
            var temporary = _pendingPath + ".tmp";
            File.WriteAllText(temporary, _lastChangeUtc.ToString("O"));
            File.Move(temporary, _pendingPath, true);
        }
        catch { }
    }

    private static void Publish(string message)
    {
        Action<string>? callback;
        lock (Gate)
        {
            _status = message;
            callback = _statusChanged;
        }
        try { callback?.Invoke(message); } catch { }
    }
}
