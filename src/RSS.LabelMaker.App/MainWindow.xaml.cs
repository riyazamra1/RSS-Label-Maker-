using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Printing;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Text;

namespace RSS.LabelMaker.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ProductRow> _products = new();
    private readonly DispatcherTimer _clockTimer = new();
    private readonly DispatcherTimer _sessionTimer = new();
    private bool _isRestoringSession;
    private string _lastActivePage = "Dashboard";
    private ClockSettings _settings = new();
    private TextBlock? _clockDate;
    private TextBlock? _clockTime;
    private TextBlock? _clockDay;
    private CheckBox? _showDate;
    private CheckBox? _showTime;
    private CheckBox? _showSeconds;
    private CheckBox? _showDayName;
    private CheckBox? _autoFitText;
    private CheckBox? _barcodeEnabled;
    private CheckBox? _qrEnabled;
    private ComboBox? _dateFormat;
    private ComboBox? _timeFormat;
    private ComboBox? _timeZone;
    private TextBox? _minimumFontSize;
    private TextBox? _maximumFontSize;
    private CheckBox? _automaticUpdates;
    private TextBlock? _updateStatus;
    private Button? _accountButton;
    private RssAccountSession? _accountSession;
    private long? _editingProductId;
    private Border? _verificationBanner;

    public MainWindow()
    {
        InitializeComponent();
        ProductsGrid.ItemsSource = _products;
        InitializeProductDatabase();
        LoadProducts();
        RefreshPrinters();
        ShowPage("Dashboard");
        BuildClockAndSettingsUi();
        LoadAppSettings();
        WireDesignerOptionPersistence();
        UpdateClock();
        InitializeSessionRecovery();
        AddVerificationBanner();
        Loaded += async (_, _) =>
        {
            try
            {
                _accountSession = await RssCoreAccountClient.GetSessionAsync();
                if (_accountSession is null)
                {
                    var account = new AccountWindow { Owner = this };
                    if (account.ShowDialog() != true)
                    {
                        Close();
                        return;
                    }
                    _accountSession = await RssCoreAccountClient.GetSessionAsync();
                    if (_accountSession is null)
                    {
                        Close();
                        return;
                    }
                }
                ApplyAccountAccess();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "RSS account service could not be reached. Sign-in is required to use RSS Label Maker.\n\n" + ex.Message,
                    "RSS Account Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }
            await RefreshAccountButtonAsync();
            UpdateService.MarkStartupHealthy();
            await ShowPendingUpdateWelcomeAsync();
            await CheckForUpdatesAsync(false);
        };

        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        Closed += (_, _) =>
        {
            SaveSessionSnapshot();
            _clockTimer.Stop();
            _sessionTimer.Stop();
        };
    }

    private void BuildClockAndSettingsUi()
    {
        // Add the live clock to the existing top-right application header.
        if (Content is Grid root && root.Children.Count > 1 && root.Children[1] is Grid appGrid)
        {
            var header = appGrid.Children.OfType<Border>().FirstOrDefault();
            if (header?.Child is Grid headerGrid)
            {
                var existingRightPanel = headerGrid.Children.OfType<StackPanel>()
                    .FirstOrDefault(panel => panel.HorizontalAlignment == HorizontalAlignment.Right);
                if (existingRightPanel is not null)
                {
                    existingRightPanel.Margin = new Thickness(0, 0, 150, 0);
                    _accountButton = new Button
                    {
                        Content = "Sign in",
                        Padding = new Thickness(12, 6, 12, 6),
                        Margin = new Thickness(0, 0, 10, 0),
                        Background = Brush("#C69A43"),
                        Foreground = Brush("#20242B"),
                        FontWeight = FontWeights.SemiBold,
                        BorderThickness = new Thickness(0),
                        Cursor = System.Windows.Input.Cursors.Hand
                    };
                    _accountButton.Click += async (_, _) =>
                    {
                        var account = new AccountWindow { Owner = this };
                        account.ShowDialog();
                        await RefreshAccountButtonAsync();
                        if (_accountSession is null) Close();
                    };
                    existingRightPanel.Children.Insert(0, _accountButton);
                }

                var clockPanel = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Width = 176,
                    Margin = new Thickness(0, 0, 0, 0)
                };
                _clockDate = new TextBlock
                {
                    FontSize = 10,
                    Foreground = Brush("#77808C"),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                _clockTime = new TextBlock
                {
                    FontSize = 17,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brush("#20242B"),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                _clockDay = new TextBlock
                {
                    FontSize = 9,
                    Foreground = Brush("#98702B"),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                clockPanel.Children.Add(_clockDate);
                clockPanel.Children.Add(_clockTime);
                clockPanel.Children.Add(_clockDay);
                headerGrid.Children.Add(clockPanel);
            }
        }

        BuildSettingsPage();
        BuildDesignerOptions();
    }


    private async Task RefreshAccountButtonAsync()
    {
        if (_accountButton is null) return;
        try
        {
            _accountSession = await RssCoreAccountClient.GetSessionAsync();
            ApplyAccountAccess();
            _accountButton.Content = _accountSession is null ? "Sign in" : "My account";
            _accountButton.ToolTip = _accountSession is null
                ? "Sign in to RSS Core."
                : $"Signed in as {_accountSession.Email}" + (_accountSession.EmailVerified ? " (verified)" : " (email not verified)");
        }
        catch
        {
            _accountButton.Content = "Account unavailable";
            _accountButton.ToolTip = "RSS Core account services require an internet connection.";
        }
    }

    private void AddVerificationBanner()
    {
        if (DashboardPage.Content is not StackPanel dashboard) return;
        _verificationBanner = new Border
        {
            Background = Brush("#FFF4D6"),
            BorderBrush = Brush("#E6C56C"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 16),
            Visibility = Visibility.Collapsed,
            Child = new TextBlock
            {
                Text = "EMAIL VERIFICATION REQUIRED — You can use the dashboard, but product creation, label editing and printing remain locked until you verify your email. Use My Account to resend the verification email.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brush("#76520C"),
                FontSize = 12
            }
        };
        dashboard.Children.Insert(0, _verificationBanner);
    }

    private bool HasActiveLicense()
    {
        if (_accountSession is null || !string.Equals(_accountSession.LicenseStatus, "active", StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.IsNullOrWhiteSpace(_accountSession.ExpiresAt)) return true;
        return DateTimeOffset.TryParse(_accountSession.ExpiresAt, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var expiry) && expiry > DateTimeOffset.UtcNow;
    }

    private bool CanUseLabelFeatures() => _accountSession?.EmailVerified == true && HasActiveLicense();

    private void ApplyAccountAccess()
    {
        var allowed = CanUseLabelFeatures();
        if (_verificationBanner is not null)
        {
            _verificationBanner.Visibility = allowed ? Visibility.Collapsed : Visibility.Visible;
            if (_verificationBanner.Child is TextBlock message)
            {
                message.Text = _accountSession?.EmailVerified != true
                    ? "EMAIL VERIFICATION REQUIRED — You can use the dashboard, but product creation, label editing and printing remain locked until you verify your email. Use My Account to resend the verification email."
                    : "LICENCE EXPIRED OR INACTIVE — Your dashboard remains available, but label creation, editing and printing require an active RSS Label Maker licence.";
            }
        }
        if (!allowed && new[] { "Products", "Designer", "Templates", "Print", "Import" }.Contains(_lastActivePage, StringComparer.Ordinal))
        {
            _lastActivePage = "Dashboard";
            ShowPage("Dashboard");
        }
    }

    private bool RequireVerifiedEmail()
    {
        if (CanUseLabelFeatures()) return true;
        var message = _accountSession?.EmailVerified != true
            ? "Please verify your RSS account email address first. You can still use the dashboard and resend the verification email from My Account."
            : "Your RSS Label Maker trial or licence is expired or inactive. An active licence is required to create, edit or print labels.";
        MessageBox.Show(this, message, "RSS Label Maker Access Required",
            MessageBoxButton.OK, MessageBoxImage.Information);
        ShowPage("Dashboard");
        return false;
    }

    private void BuildSettingsPage()
    {
        var settingsBorder = SettingsPage.Children.OfType<Border>().FirstOrDefault();
        if (settingsBorder is null) return;

        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = "APPLICATION SETTINGS",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#20242B"),
            Margin = new Thickness(0, 0, 0, 5)
        });
        content.Children.Add(new TextBlock
        {
            Text = "Changes are stored locally on this PC.",
            FontSize = 12,
            Foreground = Brush("#77808C"),
            Margin = new Thickness(0, 0, 0, 18)
        });

        _showDate = SettingCheckBox("Show date in top-right corner");
        _showTime = SettingCheckBox("Show time in top-right corner");
        _showSeconds = SettingCheckBox("Show seconds");
        _showDayName = SettingCheckBox("Show day name");
        _dateFormat = SettingComboBox(new[]
        {
            "dd MMMM yyyy", "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd"
        });
        _timeFormat = SettingComboBox(new[] { "12-hour (AM/PM)", "24-hour" });
        _timeZone = new ComboBox
        {
            Margin = new Thickness(0, 4, 0, 10),
            Padding = new Thickness(8, 6, 8, 6),
            ItemsSource = new[]
            {
                new TimeZoneOption("Sri Lanka — Asia/Colombo", "Sri Lanka Standard Time"),
                new TimeZoneOption("Use Windows local time zone", "LOCAL"),
                new TimeZoneOption("India — Asia/Kolkata", "India Standard Time"),
                new TimeZoneOption("UTC", "UTC"),
                new TimeZoneOption("United Kingdom — Europe/London", "GMT Standard Time"),
                new TimeZoneOption("United States — America/New_York", "Eastern Standard Time")
            },
            DisplayMemberPath = "Name",
            SelectedValuePath = "Id"
        };

        content.Children.Add(CreateSettingsSection("DATE & TIME",
            SettingLabel("Date format"), _dateFormat,
            SettingLabel("Time format"), _timeFormat,
            SettingLabel("Time zone"), _timeZone,
            _showDate, _showTime, _showSeconds, _showDayName));

        _automaticUpdates = SettingCheckBox("Automatically check, download and install updates at startup");
        _updateStatus = new TextBlock
        {
            Text = $"Current version: {UpdateService.GetCurrentVersion()} · Update status: not checked yet.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Foreground = Brush("#77808C"),
            Margin = new Thickness(0, 4, 0, 8)
        };
        var checkNow = new Button
        {
            Content = "Check for updates now",
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(14, 7, 14, 7)
        };
        checkNow.Click += async (_, _) => await CheckForUpdatesAsync(true);
        content.Children.Add(CreateSettingsSection("APPLICATION UPDATES",
            _automaticUpdates, _updateStatus, checkNow,
            new TextBlock
            {
                Text = "Updates are downloaded only from the RSS Label Maker GitHub Releases page. A separate updater replaces the application and restarts it. Internet access is required; offline label work remains available.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 10,
                Foreground = Brush("#77808C")
            }));

        _autoFitText = SettingCheckBox("Auto-fit text to label (default)");
        _minimumFontSize = SettingTextBox("6");
        _maximumFontSize = SettingTextBox("48");
        var fontLimits = new Grid();
        fontLimits.ColumnDefinitions.Add(new ColumnDefinition());
        fontLimits.ColumnDefinitions.Add(new ColumnDefinition());
        var minPanel = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
        minPanel.Children.Add(SettingLabel("Minimum font size (pt)"));
        minPanel.Children.Add(_minimumFontSize);
        var maxPanel = new StackPanel();
        maxPanel.Children.Add(SettingLabel("Maximum font size (pt)"));
        maxPanel.Children.Add(_maximumFontSize);
        Grid.SetColumn(maxPanel, 1);
        fontLimits.Children.Add(minPanel);
        fontLimits.Children.Add(maxPanel);
        content.Children.Add(CreateSettingsSection("TEXT SIZE & AUTO-FIT",
            _autoFitText, new TextBlock
            {
                Text = "Auto-fit is the default. Manual sizing remains available per text element. If text cannot fit at the minimum size, the designer must warn instead of silently clipping it.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = Brush("#77808C"),
                Margin = new Thickness(0, 4, 0, 10)
            }, fontLimits));

        var backupButton = new Button
        {
            Content = "Back up local database",
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(14, 7, 14, 7),
            Margin = new Thickness(0, 0, 0, 8)
        };
        backupButton.Click += BackupDatabase_Click;
        var restoreButton = new Button
        {
            Content = "Restore database backup",
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(14, 7, 14, 7)
        };
        restoreButton.Click += RestoreDatabase_Click;
        content.Children.Add(CreateSettingsSection("LOCAL DATA BACKUP & RESTORE",
            new TextBlock
            {
                Text = "Back up products and local records to a SQLite database file. Restore validates the backup and creates a safety copy of the current database first.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = Brush("#77808C"),
                Margin = new Thickness(0, 0, 0, 10)
            }, backupButton, restoreButton));

        content.Children.Add(CreateSettingsSection("LABEL DESIGN DEFAULTS",
            new TextBlock
            {
                Text = "Barcode and QR are optional, independent elements. Their controls are available in the Label Designer and both start disabled.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = Brush("#77808C")
            }));

        var save = new Button
        {
            Content = "Save Settings",
            Background = Brush("#C69A43"),
            Foreground = Brush("#20242B"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 8),
            Padding = new Thickness(20, 10, 20, 10)
        };
        save.Click += SaveSettings_Click;
        content.Children.Add(save);

        var status = new TextBlock
        {
            Name = "SettingsSaveStatus",
            Text = "Settings are saved on this PC.",
            Foreground = Brush("#4E7B5C"),
            FontSize = 11,
            Margin = new Thickness(0, 2, 0, 0)
        };
        content.Children.Add(status);

        settingsBorder.Child = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
    }

    private void BuildDesignerOptions()
    {
        var designerPanel = DesignerPage.Children.OfType<Border>().FirstOrDefault()?.Child as StackPanel;
        if (designerPanel is null) return;

        designerPanel.Children.Add(new Separator { Margin = new Thickness(0, 14, 0, 12) });
        designerPanel.Children.Add(new TextBlock
        {
            Text = "OPTIONAL ELEMENTS",
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Foreground = Brush("#77808C"),
            Margin = new Thickness(0, 0, 0, 8)
        });
        _barcodeEnabled = SettingCheckBox("Enable barcode");
        _qrEnabled = SettingCheckBox("Enable QR code");
        designerPanel.Children.Add(_barcodeEnabled);
        designerPanel.Children.Add(_qrEnabled);
        designerPanel.Children.Add(new TextBlock
        {
            Text = "Both are off by default. Enable only the elements required by this label.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 10,
            Foreground = Brush("#77808C"),
            Margin = new Thickness(0, 5, 0, 0)
        });
    }

    private static Border CreateSettingsSection(string heading, params UIElement[] controls)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = heading,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#98702B"),
            Margin = new Thickness(0, 0, 0, 10)
        });
        foreach (var control in controls)
            stack.Children.Add(control);

        return new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush("#E6E8EB"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 12),
            Child = stack
        };
    }

    private static TextBlock SettingLabel(string text) => new()
    {
        Text = text,
        FontSize = 11,
        Foreground = Brush("#77808C"),
        Margin = new Thickness(0, 5, 0, 0)
    };

    private static CheckBox SettingCheckBox(string text) => new()
    {
        Content = text,
        FontSize = 12,
        Margin = new Thickness(0, 5, 0, 5),
        IsChecked = false
    };

    private static ComboBox SettingComboBox(IEnumerable<string> values)
    {
        var combo = new ComboBox
        {
            Margin = new Thickness(0, 4, 0, 10),
            Padding = new Thickness(8, 6, 8, 6),
            ItemsSource = values
        };
        return combo;
    }

    private static TextBox SettingTextBox(string value) => new()
    {
        Text = value,
        Margin = new Thickness(0, 4, 0, 10),
        Padding = new Thickness(8, 6, 8, 6),
        MinWidth = 90
    };

    private static SolidColorBrush Brush(string color) =>
        new((Color)ColorConverter.ConvertFromString(color));

    private void WireDesignerOptionPersistence()
    {
        if (_barcodeEnabled is not null)
        {
            _barcodeEnabled.Checked += (_, _) => SaveSettingsSilently();
            _barcodeEnabled.Unchecked += (_, _) => SaveSettingsSilently();
        }
        if (_qrEnabled is not null)
        {
            _qrEnabled.Checked += (_, _) => SaveSettingsSilently();
            _qrEnabled.Unchecked += (_, _) => SaveSettingsSilently();
        }
        if (_automaticUpdates is not null)
        {
            _automaticUpdates.Checked += (_, _) => SaveSettingsSilently();
            _automaticUpdates.Unchecked += (_, _) => SaveSettingsSilently();
        }
    }

    private void LoadAppSettings()
    {
        try
        {
            var path = GetSettingsPath();
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<ClockSettings>(File.ReadAllText(path));
                if (loaded is not null) _settings = loaded;
            }
        }
        catch
        {
            _settings = new ClockSettings();
        }

        if (_showDate is not null) _showDate.IsChecked = _settings.ShowDate;
        if (_showTime is not null) _showTime.IsChecked = _settings.ShowTime;
        if (_showSeconds is not null) _showSeconds.IsChecked = _settings.ShowSeconds;
        if (_showDayName is not null) _showDayName.IsChecked = _settings.ShowDayName;
        if (_dateFormat is not null) _dateFormat.SelectedItem = _settings.DateFormat;
        if (_timeFormat is not null) _timeFormat.SelectedItem = _settings.TimeFormat;
        if (_timeZone is not null) _timeZone.SelectedValue = _settings.TimeZoneId;
        if (_autoFitText is not null) _autoFitText.IsChecked = _settings.AutoFitText;
        if (_minimumFontSize is not null) _minimumFontSize.Text = _settings.MinimumFontSize.ToString();
        if (_maximumFontSize is not null) _maximumFontSize.Text = _settings.MaximumFontSize.ToString();
        if (_barcodeEnabled is not null) _barcodeEnabled.IsChecked = _settings.BarcodeEnabled;
        if (_qrEnabled is not null) _qrEnabled.IsChecked = _settings.QrEnabled;
        if (_automaticUpdates is not null) _automaticUpdates.IsChecked = _settings.AutomaticUpdatesEnabled;
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e) => SaveSettingsSilently();

    private void SaveSettingsSilently()
    {
        try
        {
            _settings.ShowDate = _showDate?.IsChecked == true;
            _settings.ShowTime = _showTime?.IsChecked == true;
            _settings.ShowSeconds = _showSeconds?.IsChecked == true;
            _settings.ShowDayName = _showDayName?.IsChecked == true;
            _settings.DateFormat = _dateFormat?.SelectedItem as string ?? "dd MMMM yyyy";
            _settings.TimeFormat = _timeFormat?.SelectedItem as string ?? "12-hour (AM/PM)";
            _settings.TimeZoneId = _timeZone?.SelectedValue as string ?? "Sri Lanka Standard Time";
            _settings.AutoFitText = _autoFitText?.IsChecked != false;
            _settings.MinimumFontSize = ParseFontSize(_minimumFontSize?.Text, 6);
            _settings.MaximumFontSize = ParseFontSize(_maximumFontSize?.Text, 48);
            _settings.BarcodeEnabled = _barcodeEnabled?.IsChecked == true;
            _settings.QrEnabled = _qrEnabled?.IsChecked == true;
            _settings.AutomaticUpdatesEnabled = _automaticUpdates?.IsChecked != false;

            if (_settings.MinimumFontSize <= 0 || _settings.MaximumFontSize < _settings.MinimumFontSize)
                throw new InvalidOperationException("Maximum font size must be greater than or equal to the positive minimum font size.");

            var path = GetSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));
            UpdateClock();

            if (SettingsPage.Children.OfType<Border>().FirstOrDefault()?.Child is ScrollViewer scroll &&
                scroll.Content is StackPanel stack &&
                stack.Children.OfType<TextBlock>().LastOrDefault() is TextBlock status)
                status.Text = "Settings saved successfully on this PC.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Settings could not be saved", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task ShowPendingUpdateWelcomeAsync()
    {
        var info = UpdateService.ReadPendingUpdate();
        if (info is null) return;

        var dialog = new UpdateWelcomeWindow(info) { Owner = this };
        var acknowledged = dialog.ShowDialog() == true;
        if (acknowledged) UpdateService.AcknowledgeUpdate();
    }

    private async Task CheckForUpdatesAsync(bool userRequested)
    {
        if (!userRequested && !_settings.AutomaticUpdatesEnabled) return;
        if (_updateStatus is not null) _updateStatus.Text = $"Current version: {UpdateService.GetCurrentVersion()} · Checking for updates…";

        try
        {
            var progress = new Progress<string>(message =>
            {
                if (_updateStatus is not null) _updateStatus.Text = $"Current version: {UpdateService.GetCurrentVersion()} · {message}";
            });
            var result = await UpdateService.CheckAndStageAsync(progress);
            if (result is null)
            {
                if (_updateStatus is not null) _updateStatus.Text = $"Current version: {UpdateService.GetCurrentVersion()} · You are using the latest published version, or no stable release is available yet.";
                return;
            }

            if (_updateStatus is not null)
                _updateStatus.Text = $"Current version: {UpdateService.GetCurrentVersion()} · Version {result.Version} is verified and ready. Windows may request permission to install it.";
            SaveSessionSnapshot();
            UpdateService.StartUpdater(result, Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the running application."), Environment.ProcessId);
            _sessionTimer.Stop();
            _clockTimer.Stop();
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            if (_updateStatus is not null) _updateStatus.Text = "Update check/install was not completed: " + ex.Message;
            if (userRequested)
                MessageBox.Show(this, ex.Message, "RSS Label Maker update", MessageBoxButton.OK, MessageBoxImage.Warning);
            System.Diagnostics.Debug.WriteLine("RLM update failed: " + ex);
        }
    }

    private void UpdateClock()
    {
        if (_clockDate is null || _clockTime is null || _clockDay is null) return;

        TimeZoneInfo zone;
        try
        {
            zone = _settings.TimeZoneId == "LOCAL"
                ? TimeZoneInfo.Local
                : TimeZoneInfo.FindSystemTimeZoneById(_settings.TimeZoneId);
        }
        catch
        {
            zone = TimeZoneInfo.Local;
        }

        var localTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
        _clockDate.Text = localTime.ToString(_settings.DateFormat);
        _clockTime.Text = localTime.ToString(_settings.ShowSeconds
            ? (_settings.TimeFormat.StartsWith("24", StringComparison.Ordinal) ? "HH:mm:ss" : "hh:mm:ss tt")
            : (_settings.TimeFormat.StartsWith("24", StringComparison.Ordinal) ? "HH:mm" : "hh:mm tt"));
        _clockDay.Text = _settings.ShowDayName ? localTime.ToString("dddd") + " · " + zone.Id : zone.Id;
        _clockDate.Visibility = _settings.ShowDate ? Visibility.Visible : Visibility.Collapsed;
        _clockTime.Visibility = _settings.ShowTime ? Visibility.Visible : Visibility.Collapsed;
        _clockDay.Visibility = _settings.ShowDayName ? Visibility.Visible : Visibility.Collapsed;
    }

    private static int ParseFontSize(string? value, int fallback) =>
        int.TryParse(value, out var parsed) ? parsed : fallback;

    private static string GetSettingsPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Razeen Secure Solution", "RSS Label Maker", "settings.json");

    private void InitializeSessionRecovery()
    {
        _sessionTimer.Interval = TimeSpan.FromSeconds(2);
        _sessionTimer.Tick += (_, _) => SaveSessionSnapshot();

        _isRestoringSession = true;
        RestoreSessionSnapshot();
        _isRestoringSession = false;

        foreach (var input in new[]
        {
            EnglishName, SinhalaName, TamilName, Mfd, Expiry, Batch, NetWeight,
            Sugar, Salt, Fat, Mrp, Importer, Copies, LabelWidthInput, LabelHeightInput
        })
            input.TextChanged += SessionInputChanged;

        foreach (var check in new[] { PrintMfd, PrintExpiry, PrintBatch, PrintNetWeight })
        {
            check.Checked += SessionInputChanged;
            check.Unchecked += SessionInputChanged;
        }

        ProductCategory.SelectionChanged += SessionSelectionChanged;
        LabelPaperSize.SelectionChanged += SessionSelectionChanged;
        ProductsGrid.SelectionChanged += SessionSelectionChanged;
        _sessionTimer.Start();
    }

    private void SessionInputChanged(object sender, RoutedEventArgs e)
    {
        if (!_isRestoringSession) SaveSessionSnapshot();
    }

    private void SessionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isRestoringSession) SaveSessionSnapshot();
    }

    private void SaveSessionSnapshot()
    {
        if (_isRestoringSession || !IsInitialized) return;

        try
        {
            var snapshot = new SessionSnapshot
            {
                SavedUtc = DateTimeOffset.UtcNow,
                LastPage = _lastActivePage,
                English = EnglishName.Text,
                Sinhala = SinhalaName.Text,
                Tamil = TamilName.Text,
                Mfd = Mfd.Text,
                Expiry = Expiry.Text,
                Batch = Batch.Text,
                NetWeight = NetWeight.Text,
                Sugar = Sugar.Text,
                Salt = Salt.Text,
                Fat = Fat.Text,
                Mrp = Mrp.Text,
                Importer = Importer.Text,
                Copies = Copies.Text,
                CategoryIndex = ProductCategory.SelectedIndex,
                PrintMfd = PrintMfd.IsChecked == true,
                PrintExpiry = PrintExpiry.IsChecked == true,
                PrintBatch = PrintBatch.IsChecked == true,
                PrintNetWeight = PrintNetWeight.IsChecked == true,
                LabelWidth = LabelWidthInput.Text,
                LabelHeight = LabelHeightInput.Text,
                PaperIndex = LabelPaperSize.SelectedIndex,
                CustomPaperWidth = CustomPaperWidth.Text, CustomPaperHeight = CustomPaperHeight.Text,
                MarginTop = MarginTopInput.Text, MarginRight = MarginRightInput.Text,
                MarginBottom = MarginBottomInput.Text, MarginLeft = MarginLeftInput.Text,
                GapHorizontal = GapHorizontalInput.Text, GapVertical = GapVerticalInput.Text,
                SelectedProductIndex = ProductsGrid.SelectedIndex,
                WindowWidth = Width,
                WindowHeight = Height,
                IsMaximized = WindowState == WindowState.Maximized
            };

            var path = GetSessionPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, path, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RLM session checkpoint failed: {ex}");
        }
    }

    private void RestoreSessionSnapshot()
    {
        var path = GetSessionPath();
        if (!File.Exists(path)) return;

        try
        {
            var snapshot = JsonSerializer.Deserialize<SessionSnapshot>(File.ReadAllText(path));
            if (snapshot is null) return;

            EnglishName.Text = snapshot.English;
            SinhalaName.Text = snapshot.Sinhala;
            TamilName.Text = snapshot.Tamil;
            Mfd.Text = snapshot.Mfd;
            Expiry.Text = snapshot.Expiry;
            Batch.Text = snapshot.Batch;
            NetWeight.Text = snapshot.NetWeight;
            Sugar.Text = snapshot.Sugar;
            Salt.Text = snapshot.Salt;
            Fat.Text = snapshot.Fat;
            Mrp.Text = snapshot.Mrp;
            Importer.Text = snapshot.Importer;
            Copies.Text = snapshot.Copies;
            if (snapshot.CategoryIndex >= 0 && snapshot.CategoryIndex < ProductCategory.Items.Count)
                ProductCategory.SelectedIndex = snapshot.CategoryIndex;
            PrintMfd.IsChecked = snapshot.PrintMfd;
            PrintExpiry.IsChecked = snapshot.PrintExpiry;
            PrintBatch.IsChecked = snapshot.PrintBatch;
            PrintNetWeight.IsChecked = snapshot.PrintNetWeight;
            LabelWidthInput.Text = snapshot.LabelWidth;
            LabelHeightInput.Text = snapshot.LabelHeight;
            if (snapshot.PaperIndex >= 0 && snapshot.PaperIndex < LabelPaperSize.Items.Count)
                LabelPaperSize.SelectedIndex = snapshot.PaperIndex;
            CustomPaperWidth.Text = snapshot.CustomPaperWidth;
            CustomPaperHeight.Text = snapshot.CustomPaperHeight;
            MarginTopInput.Text = snapshot.MarginTop; MarginRightInput.Text = snapshot.MarginRight;
            MarginBottomInput.Text = snapshot.MarginBottom; MarginLeftInput.Text = snapshot.MarginLeft;
            GapHorizontalInput.Text = snapshot.GapHorizontal; GapVerticalInput.Text = snapshot.GapVertical;
            if (snapshot.SelectedProductIndex >= 0 && snapshot.SelectedProductIndex < ProductsGrid.Items.Count)
                ProductsGrid.SelectedIndex = snapshot.SelectedProductIndex;
            if (snapshot.WindowWidth >= MinWidth && snapshot.WindowWidth <= 10000) Width = snapshot.WindowWidth;
            if (snapshot.WindowHeight >= MinHeight && snapshot.WindowHeight <= 10000) Height = snapshot.WindowHeight;
            WindowState = snapshot.IsMaximized ? WindowState.Maximized : WindowState.Normal;

            var page = snapshot.LastPage;
            var allowedPages = new[] { "Dashboard", "Products", "Designer", "Templates", "Print", "Import", "Settings" };
            if (allowedPages.Contains(page, StringComparer.Ordinal))
                ShowPage(page);

            ProductStatus.Text = $"Recovered your last working session ({snapshot.SavedUtc.ToLocalTime():g}).";
        }
        catch (Exception ex)
        {
            ProductStatus.Text = "A previous session checkpoint could not be restored. Your saved products remain safe.";
            System.Diagnostics.Debug.WriteLine($"RLM session recovery failed: {ex}");
        }
    }

    private static string GetSessionPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Razeen Secure Solution", "RSS Label Maker", "session-recovery.json");

    private sealed class SessionSnapshot
    {
        public DateTimeOffset SavedUtc { get; set; }
        public string LastPage { get; set; } = "Dashboard";
        public string English { get; set; } = "";
        public string Sinhala { get; set; } = "";
        public string Tamil { get; set; } = "";
        public string Mfd { get; set; } = "";
        public string Expiry { get; set; } = "";
        public string Batch { get; set; } = "";
        public string NetWeight { get; set; } = "";
        public string Sugar { get; set; } = "";
        public string Salt { get; set; } = "";
        public string Fat { get; set; } = "";
        public string Mrp { get; set; } = "";
        public string Importer { get; set; } = "";
        public string Copies { get; set; } = "1";
        public int CategoryIndex { get; set; }
        public bool PrintMfd { get; set; }
        public bool PrintExpiry { get; set; }
        public bool PrintBatch { get; set; }
        public bool PrintNetWeight { get; set; }
        public string LabelWidth { get; set; } = "70";
        public string LabelHeight { get; set; } = "45";
        public int PaperIndex { get; set; }
        public string CustomPaperWidth { get; set; } = "210";
        public string CustomPaperHeight { get; set; } = "297";
        public string MarginTop { get; set; } = "5";
        public string MarginRight { get; set; } = "5";
        public string MarginBottom { get; set; } = "5";
        public string MarginLeft { get; set; } = "5";
        public string GapHorizontal { get; set; } = "3";
        public string GapVertical { get; set; } = "3";
        public int SelectedProductIndex { get; set; } = -1;
        public double WindowWidth { get; set; }
        public double WindowHeight { get; set; }
        public bool IsMaximized { get; set; }
    }

    private void CloseApplication_Click(object sender, RoutedEventArgs e)
    {
        SaveSessionSnapshot();
        Close();
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string page)
            ShowPage(page);
    }

    private void ShowPage(string page)
    {
        if (page != "Dashboard" && _accountSession is null)
        {
            page = "Dashboard";
        }
        else if (page != "Dashboard" && !CanUseLabelFeatures())
        {
            RequireVerifiedEmail();
            return;
        }
        _lastActivePage = page;
        if (page == "Templates") RefreshSavedTemplates();
        DashboardPage.Visibility = page == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
        ProductsPage.Visibility = page == "Products" ? Visibility.Visible : Visibility.Collapsed;
        DesignerPage.Visibility = page == "Designer" ? Visibility.Visible : Visibility.Collapsed;
        TemplatesPage.Visibility = page == "Templates" ? Visibility.Visible : Visibility.Collapsed;
        PrintPage.Visibility = page == "Print" ? Visibility.Visible : Visibility.Collapsed;
        ImportPage.Visibility = page == "Import" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;

        PageTitle.Text = page switch
        {
            "Designer" => "Label Designer",
            "Products" => "Products",
            "Templates" => "Templates",
            "Print" => "Print Manager",
            "Import" => "Import / Export",
            "Settings" => "Settings",
            _ => "Dashboard"
        };
        PageSubtitle.Text = page switch
        {
            "Designer" => "Edit your food label layout and sheet dimensions",
            "Products" => "Manage product names, dates, prices and nutrition values",
            "Templates" => "Choose a starting layout for your labels",
            "Print" => "Select a Windows printer and prepare accurate output",
            "Import" => "Bring product data in and export finished labels",
            "Settings" => "Configure this local application",
            _ => "Your label production workspace"
        };
        var buttons = new[] { NavDashboard, NavProducts, NavDesigner, NavTemplates, NavPrint, NavImport, NavSettings };
        foreach (var item in buttons)
        {
            var selected = Equals(item.Tag?.ToString(), page);
            item.Background = selected ? Brushes.DimGray : Brushes.Transparent;
            item.Foreground = Brushes.WhiteSmoke;
        }
    }

    private void OpenDesigner_Click(object sender, RoutedEventArgs e) => ShowPage("Designer");
    private void OpenProducts_Click(object sender, RoutedEventArgs e) => ShowPage("Products");
    private void OpenPrint_Click(object sender, RoutedEventArgs e) => ShowPage("Print");

    private void SaveProduct_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireVerifiedEmail()) return;
        if (string.IsNullOrWhiteSpace(EnglishName.Text))
        {
            ProductStatus.Text = "Enter an English product name before saving.";
            return;
        }
        var product = new ProductRow
        {
            Category = (ProductCategory.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Other",
            English = EnglishName.Text.Trim(), Sinhala = SinhalaName.Text.Trim(), Tamil = TamilName.Text.Trim(),
            Mfd = Mfd.Text.Trim(), Expiry = Expiry.Text.Trim(), Batch = Batch.Text.Trim(),
            NetWeight = NetWeight.Text.Trim(), Sugar = Sugar.Text.Trim(), Salt = Salt.Text.Trim(), Fat = Fat.Text.Trim(),
            Mrp = Mrp.Text.Trim(), Importer = Importer.Text.Trim(), Copies = ParsePositiveInt(Copies.Text, 1),
            PrintMfd = PrintMfd.IsChecked == true, PrintExpiry = PrintExpiry.IsChecked == true,
            PrintBatch = PrintBatch.IsChecked == true, PrintNetWeight = PrintNetWeight.IsChecked == true
        };
        try
        {
            using var connection = new SqliteConnection($"Data Source={GetDatabasePath()}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = _editingProductId.HasValue ? """
                UPDATE Products SET Category=$category, English=$english, Sinhala=$sinhala, Tamil=$tamil,
                    Mfd=$mfd, Expiry=$expiry, Batch=$batch, NetWeight=$weight, Sugar=$sugar, Salt=$salt,
                    Fat=$fat, Mrp=$mrp, Importer=$importer, Copies=$copies, PrintMfd=$printMfd,
                    PrintExpiry=$printExpiry, PrintBatch=$printBatch, PrintNetWeight=$printWeight
                WHERE Id=$id;
                """ : """
                INSERT INTO Products
                (Category, English, Sinhala, Tamil, Mfd, Expiry, Batch, NetWeight, Sugar, Salt, Fat, Mrp, Importer, Copies,
                 PrintMfd, PrintExpiry, PrintBatch, PrintNetWeight, CreatedUtc)
                VALUES
                ($category, $english, $sinhala, $tamil, $mfd, $expiry, $batch, $weight, $sugar, $salt, $fat, $mrp, $importer, $copies,
                 $printMfd, $printExpiry, $printBatch, $printWeight, $createdUtc);
                """;
            command.Parameters.AddWithValue("$category", product.Category);
            command.Parameters.AddWithValue("$english", product.English);
            command.Parameters.AddWithValue("$sinhala", product.Sinhala);
            command.Parameters.AddWithValue("$tamil", product.Tamil);
            command.Parameters.AddWithValue("$mfd", product.Mfd);
            command.Parameters.AddWithValue("$expiry", product.Expiry);
            command.Parameters.AddWithValue("$batch", product.Batch);
            command.Parameters.AddWithValue("$weight", product.NetWeight);
            command.Parameters.AddWithValue("$sugar", product.Sugar);
            command.Parameters.AddWithValue("$salt", product.Salt);
            command.Parameters.AddWithValue("$fat", product.Fat);
            command.Parameters.AddWithValue("$mrp", product.Mrp);
            command.Parameters.AddWithValue("$importer", product.Importer);
            command.Parameters.AddWithValue("$copies", product.Copies);
            command.Parameters.AddWithValue("$printMfd", product.PrintMfd ? 1 : 0);
            command.Parameters.AddWithValue("$printExpiry", product.PrintExpiry ? 1 : 0);
            command.Parameters.AddWithValue("$printBatch", product.PrintBatch ? 1 : 0);
            command.Parameters.AddWithValue("$printWeight", product.PrintNetWeight ? 1 : 0);
            if (_editingProductId.HasValue) command.Parameters.AddWithValue("$id", _editingProductId.Value);
            else command.Parameters.AddWithValue("$createdUtc", DateTimeOffset.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
            if (_editingProductId.HasValue)
            {
                var index = _products.ToList().FindIndex(x => x.Id == _editingProductId.Value);
                if (index >= 0) _products[index] = product with { Id = _editingProductId.Value };
                ProductStatus.Text = "Product changes updated successfully.";
            }
            else
            {
                using var idCommand = connection.CreateCommand();
                idCommand.CommandText = "SELECT last_insert_rowid();";
                var insertedId = Convert.ToInt64(idCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
                _products.Insert(0, product with { Id = insertedId });
                ProductStatus.Text = "Product saved to the local SQLite database.";
            }
            _editingProductId = null;
            ProductCount.Text = _products.Count.ToString();
            SaveProductButton.Content = "Save Product";
            UpdateLabelPreview(product);
            SaveSessionSnapshot();
        }
        catch (Exception ex) { ProductStatus.Text = $"Product was not saved: {ex.Message}"; }
    }

    private void SaveTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireVerifiedEmail()) return;
        var name = TemplateNameInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            TemplateStatus.Text = "Enter a name for this layout preset.";
            return;
        }
        var data = new LayoutTemplateData
        {
            LabelWidth = LabelWidthInput.Text, LabelHeight = LabelHeightInput.Text,
            PaperIndex = LabelPaperSize.SelectedIndex,
            CustomPaperWidth = CustomPaperWidth.Text, CustomPaperHeight = CustomPaperHeight.Text,
            MarginTop = MarginTopInput.Text, MarginRight = MarginRightInput.Text,
            MarginBottom = MarginBottomInput.Text, MarginLeft = MarginLeftInput.Text,
            GapHorizontal = GapHorizontalInput.Text, GapVertical = GapVerticalInput.Text
        };
        try
        {
            using var connection = new SqliteConnection($"Data Source={GetDatabasePath()}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO LabelTemplates(Name,DataJson,CreatedUtc,UpdatedUtc)
                VALUES($name,$data,$now,$now)
                ON CONFLICT(Name) DO UPDATE SET DataJson=excluded.DataJson, UpdatedUtc=excluded.UpdatedUtc;
                """;
            command.Parameters.AddWithValue("$name", name);
            command.Parameters.AddWithValue("$data", JsonSerializer.Serialize(data));
            command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
            RefreshSavedTemplates();
            TemplateStatus.Text = $"Saved layout preset '{name}'.";
        }
        catch (Exception ex) { TemplateStatus.Text = $"Could not save preset: {ex.Message}"; }
    }

    private void RefreshSavedTemplates()
    {
        if (SavedTemplatesList is null) return;
        try
        {
            var rows = new List<TemplateRow>();
            using var connection = new SqliteConnection($"Data Source={GetDatabasePath()}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id,Name,DataJson FROM LabelTemplates ORDER BY Name;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
                rows.Add(new TemplateRow(reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
            SavedTemplatesList.ItemsSource = rows;
        }
        catch (Exception ex) { TemplateStatus.Text = $"Could not load saved presets: {ex.Message}"; }
    }

    private void DuplicateTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireVerifiedEmail()) return;
        if (SavedTemplatesList.SelectedItem is not TemplateRow selected) return;
        try
        {
            var baseName = selected.Name + " Copy";
            var name = baseName;
            var suffix = 2;
            using var connection = new SqliteConnection($"Data Source={GetDatabasePath()}");
            connection.Open();
            while (true)
            {
                using var exists = connection.CreateCommand();
                exists.CommandText = "SELECT 1 FROM LabelTemplates WHERE Name=$name COLLATE NOCASE;";
                exists.Parameters.AddWithValue("$name", name);
                if (exists.ExecuteScalar() is null) break;
                name = $"{baseName} {suffix++}";
            }
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO LabelTemplates(Name,DataJson,CreatedUtc,UpdatedUtc) VALUES($name,$data,$now,$now);";
            insert.Parameters.AddWithValue("$name", name);
            insert.Parameters.AddWithValue("$data", selected.DataJson);
            insert.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            insert.ExecuteNonQuery();
            RefreshSavedTemplates();
            TemplateStatus.Text = $"Duplicated as '{name}'.";
        }
        catch (Exception ex) { TemplateStatus.Text = $"Could not duplicate preset: {ex.Message}"; }
    }

    private void ExportTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireVerifiedEmail()) return;
        if (SavedTemplatesList.SelectedItem is not TemplateRow selected) return;
        var dialog = new SaveFileDialog
        {
            Filter = "RSS label layout (*.rslayout.json)|*.rslayout.json",
            FileName = string.Concat(selected.Name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)) + ".rslayout.json"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var export = new ExportedTemplate { Name = selected.Name, DataJson = selected.DataJson };
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true }));
            TemplateStatus.Text = $"Exported '{selected.Name}'.";
        }
        catch (Exception ex) { TemplateStatus.Text = $"Export failed: {ex.Message}"; }
    }

    private void ImportTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireVerifiedEmail()) return;
        var dialog = new OpenFileDialog
        {
            Filter = "RSS label layout (*.rslayout.json)|*.rslayout.json|JSON files (*.json)|*.json",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var imported = JsonSerializer.Deserialize<ExportedTemplate>(File.ReadAllText(dialog.FileName))
                ?? throw new InvalidDataException("The selected file is not a valid RSS layout preset.");
            if (string.IsNullOrWhiteSpace(imported.Name) || string.IsNullOrWhiteSpace(imported.DataJson))
                throw new InvalidDataException("The layout preset is missing its name or data.");
            var data = JsonSerializer.Deserialize<LayoutTemplateData>(imported.DataJson)
                ?? throw new InvalidDataException("The layout preset data is invalid.");
            if (!double.TryParse(data.LabelWidth, NumberStyles.Number, CultureInfo.InvariantCulture, out var w) || w <= 0 ||
                !double.TryParse(data.LabelHeight, NumberStyles.Number, CultureInfo.InvariantCulture, out var h) || h <= 0)
                throw new InvalidDataException("The layout dimensions are invalid.");
            using var connection = new SqliteConnection($"Data Source={GetDatabasePath()}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO LabelTemplates(Name,DataJson,CreatedUtc,UpdatedUtc)
                VALUES($name,$data,$now,$now)
                ON CONFLICT(Name) DO UPDATE SET DataJson=excluded.DataJson, UpdatedUtc=excluded.UpdatedUtc;
                """;
            command.Parameters.AddWithValue("$name", imported.Name.Trim());
            command.Parameters.AddWithValue("$data", JsonSerializer.Serialize(data));
            command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
            RefreshSavedTemplates();
            TemplateStatus.Text = $"Imported '{imported.Name}'.";
        }
        catch (Exception ex) { TemplateStatus.Text = $"Import failed: {ex.Message}"; }
    }

    private void LoadTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireVerifiedEmail()) return;
        if (SavedTemplatesList.SelectedItem is not TemplateRow selected) return;
        try
        {
            var data = JsonSerializer.Deserialize<LayoutTemplateData>(selected.DataJson)
                ?? throw new InvalidDataException("Preset data is empty.");
            LabelWidthInput.Text = data.LabelWidth; LabelHeightInput.Text = data.LabelHeight;
            if (data.PaperIndex >= 0 && data.PaperIndex < LabelPaperSize.Items.Count)
                LabelPaperSize.SelectedIndex = data.PaperIndex;
            CustomPaperWidth.Text = data.CustomPaperWidth; CustomPaperHeight.Text = data.CustomPaperHeight;
            MarginTopInput.Text = data.MarginTop; MarginRightInput.Text = data.MarginRight;
            MarginBottomInput.Text = data.MarginBottom; MarginLeftInput.Text = data.MarginLeft;
            GapHorizontalInput.Text = data.GapHorizontal; GapVerticalInput.Text = data.GapVertical;
            TemplateNameInput.Text = selected.Name;
            TemplateStatus.Text = $"Loaded '{selected.Name}'. Apply dimensions to calculate labels per page.";
            ShowPage("Designer");
        }
        catch (Exception ex) { TemplateStatus.Text = $"Could not load preset: {ex.Message}"; }
    }

    private void DeleteTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireVerifiedEmail()) return;
        if (SavedTemplatesList.SelectedItem is not TemplateRow selected) return;
        if (MessageBox.Show(this, $"Delete layout preset '{selected.Name}'?",
            "Confirm deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            using var connection = new SqliteConnection($"Data Source={GetDatabasePath()}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM LabelTemplates WHERE Id=$id;";
            command.Parameters.AddWithValue("$id", selected.Id);
            command.ExecuteNonQuery();
            RefreshSavedTemplates();
            TemplateStatus.Text = $"Deleted '{selected.Name}'.";
        }
        catch (Exception ex) { TemplateStatus.Text = $"Could not delete preset: {ex.Message}"; }
    }

    private void DeleteProduct_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireVerifiedEmail()) return;
        if (sender is not Button { DataContext: ProductRow product } || product.Id <= 0) return;
        var answer = MessageBox.Show(this,
            $"Delete product '{product.English}'? This cannot be undone.",
            "Confirm product deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        try
        {
            using var connection = new SqliteConnection($"Data Source={GetDatabasePath()}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Products WHERE Id = $id;";
            command.Parameters.AddWithValue("$id", product.Id);
            var affected = command.ExecuteNonQuery();
            if (_editingProductId == product.Id)
            {
                _editingProductId = null;
                SaveProductButton.Content = "Save Product";
                ProductStatus.Text = "The edited product was deleted.";
            }
            LoadProducts();
            ProductStatus.Text = affected == 1 ? $"Deleted '{product.English}'." : "Product was not found.";
        }
        catch (Exception ex) { ProductStatus.Text = $"Delete failed: {ex.Message}"; }
    }

    private void EditProduct_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireVerifiedEmail()) return;
        if (sender is not Button { DataContext: ProductRow product } || product.Id <= 0) return;
        _editingProductId = product.Id;
        ProductCategory.SelectedIndex = Math.Max(0, ProductCategory.Items.Cast<ComboBoxItem>().ToList()
            .FindIndex(x => string.Equals(x.Content?.ToString(), product.Category, StringComparison.Ordinal)));
        EnglishName.Text = product.English; SinhalaName.Text = product.Sinhala; TamilName.Text = product.Tamil;
        Mfd.Text = product.Mfd; Expiry.Text = product.Expiry; Batch.Text = product.Batch; NetWeight.Text = product.NetWeight;
        Sugar.Text = product.Sugar; Salt.Text = product.Salt; Fat.Text = product.Fat; Mrp.Text = product.Mrp;
        Importer.Text = product.Importer; Copies.Text = product.Copies.ToString();
        PrintMfd.IsChecked = product.PrintMfd; PrintExpiry.IsChecked = product.PrintExpiry;
        PrintBatch.IsChecked = product.PrintBatch; PrintNetWeight.IsChecked = product.PrintNetWeight;
        SaveProductButton.Content = "Update Product";
        ProductStatus.Text = $"Editing: {product.English}";
        UpdateLabelPreview(product);
        EnglishName.Focus();
    }

    private void UpdateLabelPreview(ProductRow product)
    {
        PreviewEnglish.Text = string.IsNullOrWhiteSpace(product.English) ? "PRODUCT NAME" : product.English.ToUpperInvariant();
        PreviewLocal.Text = $"{product.Sinhala}  •  {product.Tamil}".Trim(' ', '•');
        PreviewMfd.Text = $"M.F.D :  {PackValue(product.Mfd, product.PrintMfd)}";
        PreviewExpiry.Text = $"EXP.D :  {PackValue(product.Expiry, product.PrintExpiry)}";
        PreviewBatch.Text = $"B.No :  {PackValue(product.Batch, product.PrintBatch)}";
        PreviewNetWeight.Text = $"N.Wt :  {PackValue(product.NetWeight, product.PrintNetWeight)}";
        PreviewMrp.Text = $"M.R.P. (Rs.)  {Dash(product.Mrp)}";
        PreviewSugar.Text = $"{Dash(product.Sugar)} g/100g";
        PreviewSalt.Text = $"{Dash(product.Salt)} g/100g";
        PreviewFat.Text = $"{Dash(product.Fat)} g/100g";
    }

    private static string PackValue(string value, bool printedOnPack) => printedOnPack ? "Printed on Pack" : Dash(value);
    private static string Dash(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
    private static int ParsePositiveInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
    private static string GetDatabasePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Razeen Secure Solution", "RSS Label Maker", "rss-label-maker.db");

    private static void InitializeProductDatabase()
    {
        var path = GetDatabasePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Category TEXT NOT NULL DEFAULT 'Other',
                English TEXT NOT NULL, Sinhala TEXT NOT NULL DEFAULT '', Tamil TEXT NOT NULL DEFAULT '',
                Mfd TEXT NOT NULL DEFAULT '', Expiry TEXT NOT NULL DEFAULT '', Batch TEXT NOT NULL DEFAULT '',
                NetWeight TEXT NOT NULL DEFAULT '', Sugar TEXT NOT NULL DEFAULT '', Salt TEXT NOT NULL DEFAULT '',
                Fat TEXT NOT NULL DEFAULT '', Mrp TEXT NOT NULL DEFAULT '', Importer TEXT NOT NULL DEFAULT '',
                Copies INTEGER NOT NULL DEFAULT 1, PrintMfd INTEGER NOT NULL DEFAULT 0,
                PrintExpiry INTEGER NOT NULL DEFAULT 0, PrintBatch INTEGER NOT NULL DEFAULT 0,
                PrintNetWeight INTEGER NOT NULL DEFAULT 0, CreatedUtc TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
        using (var templatesCommand = connection.CreateCommand())
        {
            templatesCommand.CommandText = """
                CREATE TABLE IF NOT EXISTS LabelTemplates (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL COLLATE NOCASE UNIQUE,
                    DataJson TEXT NOT NULL,
                    CreatedUtc TEXT NOT NULL,
                    UpdatedUtc TEXT NOT NULL
                );
                """;
            templatesCommand.ExecuteNonQuery();
        }

        // Upgrade databases created by earlier versions without losing saved products.
        using var columnsCommand = connection.CreateCommand();
        columnsCommand.CommandText = "PRAGMA table_info(Products);";
        using var reader = columnsCommand.ExecuteReader();
        var hasCategory = false;
        while (reader.Read())
            if (string.Equals(reader.GetString(1), "Category", StringComparison.OrdinalIgnoreCase))
                hasCategory = true;
        reader.Close();
        if (!hasCategory)
        {
            using var migration = connection.CreateCommand();
            migration.CommandText = "ALTER TABLE Products ADD COLUMN Category TEXT NOT NULL DEFAULT 'Other';";
            migration.ExecuteNonQuery();
        }
    }

    private void LoadProducts()
    {
        _products.Clear();
        using var connection = new SqliteConnection($"Data Source={GetDatabasePath()}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Category, English, Sinhala, Tamil, Mfd, Expiry, Batch, NetWeight, Sugar, Salt, Fat, Mrp, Importer, Copies,
                   PrintMfd, PrintExpiry, PrintBatch, PrintNetWeight FROM Products ORDER BY Id DESC;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
            _products.Add(new ProductRow
            {
                Id = reader.GetInt64(0), Category = reader.GetString(1), English = reader.GetString(2), Sinhala = reader.GetString(3), Tamil = reader.GetString(4),
                Mfd = reader.GetString(5), Expiry = reader.GetString(6), Batch = reader.GetString(7),
                NetWeight = reader.GetString(8), Sugar = reader.GetString(9), Salt = reader.GetString(10),
                Fat = reader.GetString(11), Mrp = reader.GetString(12), Importer = reader.GetString(13),
                Copies = reader.GetInt32(14), PrintMfd = reader.GetInt32(15) == 1,
                PrintExpiry = reader.GetInt32(16) == 1, PrintBatch = reader.GetInt32(17) == 1,
                PrintNetWeight = reader.GetInt32(18) == 1
            });
        ProductCount.Text = _products.Count.ToString();
        if (_products.Count > 0) UpdateLabelPreview(_products[0]);
    }

    private void ApplyLayout_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireVerifiedEmail()) return;
        static bool ReadPositive(string value, out double number) =>
            double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number) &&
            double.IsFinite(number) && number > 0;
        static bool ReadNonNegative(string value, out double number) =>
            double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number) &&
            double.IsFinite(number) && number >= 0;

        if (!ReadPositive(LabelWidthInput.Text, out var labelWidth) ||
            !ReadPositive(LabelHeightInput.Text, out var labelHeight))
        {
            LayoutStatus.Text = "Enter positive label width and height in millimetres.";
            return;
        }
        var (paperWidth, paperHeight) = LabelPaperSize.SelectedIndex switch
        {
            1 => (148d, 210d),
            2 => (215.9d, 279.4d),
            3 when ReadPositive(CustomPaperWidth.Text, out var cw) &&
                   ReadPositive(CustomPaperHeight.Text, out var ch) => (cw, ch),
            3 => (0d, 0d),
            _ => (210d, 297d)
        };
        if (paperWidth <= 0 || paperHeight <= 0)
        {
            LayoutStatus.Text = "Enter valid positive custom paper width and height.";
            return;
        }
        if (!ReadNonNegative(MarginTopInput.Text, out var top) ||
            !ReadNonNegative(MarginRightInput.Text, out var right) ||
            !ReadNonNegative(MarginBottomInput.Text, out var bottom) ||
            !ReadNonNegative(MarginLeftInput.Text, out var left) ||
            !ReadNonNegative(GapHorizontalInput.Text, out var gapX) ||
            !ReadNonNegative(GapVerticalInput.Text, out var gapY))
        {
            LayoutStatus.Text = "Margins and gaps must be zero or positive numbers.";
            return;
        }
        if (left + right >= paperWidth || top + bottom >= paperHeight)
        {
            LayoutStatus.Text = "Margins leave no usable paper area.";
            return;
        }
        var usableWidth = paperWidth - left - right;
        var usableHeight = paperHeight - top - bottom;
        var columns = (int)Math.Floor((usableWidth + gapX) / (labelWidth + gapX));
        var rows = (int)Math.Floor((usableHeight + gapY) / (labelHeight + gapY));
        if (columns < 1 || rows < 1)
        {
            LayoutStatus.Text = $"A {labelWidth:0.##} × {labelHeight:0.##} mm label does not fit the usable paper area.";
            return;
        }
        var count = checked(columns * rows);
        LayoutStatus.Text = $"{paperWidth:0.##} × {paperHeight:0.##} mm · {columns} columns × {rows} rows · {count} labels/sheet. Margins T{top}/R{right}/B{bottom}/L{left} mm; gaps H{gapX}/V{gapY} mm. Printer hardware margins still require validation.";
    }

    private void ExportProductsCsv_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireVerifiedEmail()) return;
        var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "rss-label-maker-products.csv" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var rows = new List<string>
            {
                "Category,English,Sinhala,Tamil,Mfd,Expiry,Batch,NetWeight,Sugar,Salt,Fat,Mrp,Importer,Copies,PrintMfd,PrintExpiry,PrintBatch,PrintNetWeight"
            };
            rows.AddRange(_products.Select(p => string.Join(",", new[]
            {
                p.Category,p.English,p.Sinhala,p.Tamil,p.Mfd,p.Expiry,p.Batch,p.NetWeight,p.Sugar,p.Salt,p.Fat,p.Mrp,p.Importer,
                p.Copies.ToString(CultureInfo.InvariantCulture),p.PrintMfd ? "true" : "false",p.PrintExpiry ? "true" : "false",
                p.PrintBatch ? "true" : "false",p.PrintNetWeight ? "true" : "false"
            }.Select(CsvEscape))));
            File.WriteAllText(dialog.FileName, string.Join(Environment.NewLine, rows), new UTF8Encoding(true));
            ImportStatus.Text = $"Exported {_products.Count} product(s) to {dialog.FileName}.";
        }
        catch (Exception ex) { ImportStatus.Text = $"CSV export failed: {ex.Message}"; }
    }

    private void ImportProductsCsv_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireVerifiedEmail()) return;
        var dialog = new OpenFileDialog { Filter = "CSV files (*.csv)|*.csv", Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var lines = File.ReadAllLines(dialog.FileName, Encoding.UTF8);
            if (lines.Length < 2) { ImportStatus.Text = "The CSV file contains no product rows."; return; }
            var headers = ParseCsvLine(lines[0]).Select((h, i) => (Name: h.Trim(), Index: i))
                .ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase);
            if (!headers.ContainsKey("English")) { ImportStatus.Text = "CSV must contain an English column. No data was imported."; return; }
            var imported = 0; var rejected = new List<string>();
            using var connection = new SqliteConnection($"Data Source={GetDatabasePath()}");
            connection.Open();
            using var transaction = connection.BeginTransaction();
            for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                if (string.IsNullOrWhiteSpace(lines[lineIndex])) continue;
                try
                {
                    var values = ParseCsvLine(lines[lineIndex]);
                    string Read(string name, string fallback = "") =>
                        headers.TryGetValue(name, out var index) && index < values.Count ? values[index].Trim() : fallback;
                    var english = Read("English");
                    if (string.IsNullOrWhiteSpace(english)) throw new FormatException("English product name is required.");
                    var copiesText = Read("Copies", "1");
                    if (!int.TryParse(copiesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var copies) || copies < 1)
                        throw new FormatException("Copies must be a positive whole number.");
                    var product = new ProductRow
                    {
                        Category = Read("Category", "Other"), English = english, Sinhala = Read("Sinhala"), Tamil = Read("Tamil"),
                        Mfd = Read("Mfd"), Expiry = Read("Expiry"), Batch = Read("Batch"), NetWeight = Read("NetWeight"),
                        Sugar = Read("Sugar"), Salt = Read("Salt"), Fat = Read("Fat"), Mrp = Read("Mrp"), Importer = Read("Importer"),
                        Copies = copies, PrintMfd = Read("PrintMfd").Equals("true", StringComparison.OrdinalIgnoreCase) || Read("PrintMfd") == "1",
                        PrintExpiry = Read("PrintExpiry").Equals("true", StringComparison.OrdinalIgnoreCase) || Read("PrintExpiry") == "1",
                        PrintBatch = Read("PrintBatch").Equals("true", StringComparison.OrdinalIgnoreCase) || Read("PrintBatch") == "1",
                        PrintNetWeight = Read("PrintNetWeight").Equals("true", StringComparison.OrdinalIgnoreCase) || Read("PrintNetWeight") == "1"
                    };
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = """
                        INSERT INTO Products
                        (Category,English,Sinhala,Tamil,Mfd,Expiry,Batch,NetWeight,Sugar,Salt,Fat,Mrp,Importer,Copies,
                         PrintMfd,PrintExpiry,PrintBatch,PrintNetWeight,CreatedUtc)
                        VALUES ($category,$english,$sinhala,$tamil,$mfd,$expiry,$batch,$weight,$sugar,$salt,$fat,$mrp,$importer,$copies,
                         $printMfd,$printExpiry,$printBatch,$printWeight,$createdUtc);
                        """;
                    command.Parameters.AddWithValue("$category", product.Category);
                    command.Parameters.AddWithValue("$english", product.English);
                    command.Parameters.AddWithValue("$sinhala", product.Sinhala);
                    command.Parameters.AddWithValue("$tamil", product.Tamil);
                    command.Parameters.AddWithValue("$mfd", product.Mfd);
                    command.Parameters.AddWithValue("$expiry", product.Expiry);
                    command.Parameters.AddWithValue("$batch", product.Batch);
                    command.Parameters.AddWithValue("$weight", product.NetWeight);
                    command.Parameters.AddWithValue("$sugar", product.Sugar);
                    command.Parameters.AddWithValue("$salt", product.Salt);
                    command.Parameters.AddWithValue("$fat", product.Fat);
                    command.Parameters.AddWithValue("$mrp", product.Mrp);
                    command.Parameters.AddWithValue("$importer", product.Importer);
                    command.Parameters.AddWithValue("$copies", product.Copies);
                    command.Parameters.AddWithValue("$printMfd", product.PrintMfd ? 1 : 0);
                    command.Parameters.AddWithValue("$printExpiry", product.PrintExpiry ? 1 : 0);
                    command.Parameters.AddWithValue("$printBatch", product.PrintBatch ? 1 : 0);
                    command.Parameters.AddWithValue("$printWeight", product.PrintNetWeight ? 1 : 0);
                    command.Parameters.AddWithValue("$createdUtc", DateTimeOffset.UtcNow.ToString("O"));
                    command.ExecuteNonQuery();
                    imported++;
                }
                catch (Exception ex) { rejected.Add($"Row {lineIndex + 1}: {ex.Message}"); }
            }
            transaction.Commit();
            LoadProducts();
            ImportStatus.Text = $"Imported {imported} product(s). Rejected {rejected.Count} row(s)." +
                (rejected.Count == 0 ? "" : Environment.NewLine + string.Join(Environment.NewLine, rejected.Take(20)));
        }
        catch (Exception ex) { ImportStatus.Text = $"CSV import failed: {ex.Message}"; }
    }

    private static string CsvEscape(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (ch == ',' && !quoted) { values.Add(value.ToString()); value.Clear(); }
            else value.Append(ch);
        }
        if (quoted) throw new FormatException("Unclosed quoted CSV field.");
        values.Add(value.ToString());
        return values;
    }

    private void BackupDatabase_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireVerifiedEmail()) return;
        var dialog = new SaveFileDialog
        {
            Filter = "RSS Label Maker database (*.db)|*.db",
            FileName = $"rss-label-maker-backup-{DateTime.Now:yyyyMMdd-HHmmss}.db"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var sourcePath = GetDatabasePath();
            using var source = new SqliteConnection($"Data Source={sourcePath};Mode=ReadWrite");
            using var destination = new SqliteConnection($"Data Source={dialog.FileName}");
            source.Open();
            destination.Open();
            source.BackupDatabase(destination);
            PrintStatus.Text = $"Database backup created: {dialog.FileName}";
            MessageBox.Show(this, "Database backup completed.", "RSS Label Maker",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Backup failed: {ex.Message}", "RSS Label Maker",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RestoreDatabase_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireVerifiedEmail()) return;
        var dialog = new OpenFileDialog
        {
            Filter = "RSS Label Maker database (*.db)|*.db",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        if (MessageBox.Show(this,
            "Restoring replaces the current local product database. A safety backup of the current database will be created first. Continue?",
            "Confirm database restore", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var currentPath = GetDatabasePath();
        var stagedPath = currentPath + ".restore-staging";
        var safetyPath = currentPath + $".pre-restore-{DateTime.Now:yyyyMMdd-HHmmss}.bak";
        try
        {
            using (var incoming = new SqliteConnection($"Data Source={dialog.FileName};Mode=ReadOnly"))
            {
                incoming.Open();
                using var check = incoming.CreateCommand();
                check.CommandText = "PRAGMA integrity_check;";
                var integrity = Convert.ToString(check.ExecuteScalar(), CultureInfo.InvariantCulture);
                if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Backup integrity check failed: {integrity}");
                using var staged = new SqliteConnection($"Data Source={stagedPath}");
                staged.Open();
                incoming.BackupDatabase(staged);
            }

            if (File.Exists(currentPath)) File.Copy(currentPath, safetyPath, true);
            File.Copy(stagedPath, currentPath, true);
            File.Delete(stagedPath);
            LoadProducts();
            MessageBox.Show(this,
                $"Database restored. Previous database safety copy: {safetyPath}",
                "RSS Label Maker", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            try { if (File.Exists(stagedPath)) File.Delete(stagedPath); } catch { }
            MessageBox.Show(this, $"Restore failed. The original database was retained where possible. Details: {ex.Message}",
                "RSS Label Maker", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshPrinters_Click(object sender, RoutedEventArgs e) => RefreshPrinters();

    private void RefreshPrinters()
    {
        try
        {
            PrinterList.Items.Clear();
            using var server = new LocalPrintServer();
            foreach (var queue in server.GetPrintQueues())
                PrinterList.Items.Add(queue.Name);
            if (PrinterList.Items.Count > 0)
                PrinterList.SelectedIndex = 0;
            PrintStatus.Text = PrinterList.Items.Count == 0
                ? "No installed printer was found. Connect or install a Windows printer."
                : $"{PrinterList.Items.Count} installed printer(s) detected.";
        }
        catch (Exception ex)
        {
            PrintStatus.Text = $"Could not read Windows printers: {ex.Message}";
        }
    }

    private void TestPrint_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireVerifiedEmail()) return;
        PrintStatus.Text = "Printer discovery works. Actual-size label rendering and test-page printing are scheduled for the printing milestone.";
    }

    private sealed class TimeZoneOption
    {
        public TimeZoneOption(string name, string id) { Name = name; Id = id; }
        public string Name { get; }
        public string Id { get; }
    }

    private sealed class ClockSettings
    {
        public bool ShowDate { get; set; } = true;
        public bool ShowTime { get; set; } = true;
        public bool ShowSeconds { get; set; } = true;
        public bool ShowDayName { get; set; } = true;
        public string DateFormat { get; set; } = "dd MMMM yyyy";
        public string TimeFormat { get; set; } = "12-hour (AM/PM)";
        public string TimeZoneId { get; set; } = "Sri Lanka Standard Time";
        public bool AutoFitText { get; set; } = true;
        public int MinimumFontSize { get; set; } = 6;
        public int MaximumFontSize { get; set; } = 48;
        public bool BarcodeEnabled { get; set; }
        public bool QrEnabled { get; set; }
        public bool AutomaticUpdatesEnabled { get; set; } = true;
    }

    private sealed record TemplateRow(long Id, string Name, string DataJson);
    private sealed class ExportedTemplate
    {
        public string Name { get; set; } = "";
        public string DataJson { get; set; } = "";
    }


    private sealed class LayoutTemplateData
    {
        public string LabelWidth { get; set; } = "70";
        public string LabelHeight { get; set; } = "45";
        public int PaperIndex { get; set; }
        public string CustomPaperWidth { get; set; } = "210";
        public string CustomPaperHeight { get; set; } = "297";
        public string MarginTop { get; set; } = "5";
        public string MarginRight { get; set; } = "5";
        public string MarginBottom { get; set; } = "5";
        public string MarginLeft { get; set; } = "5";
        public string GapHorizontal { get; set; } = "3";
        public string GapVertical { get; set; } = "3";
    }

    private sealed record ProductRow
    {
        public long Id { get; init; }
        public string Category { get; init; } = "Other";
        public string English { get; init; } = "";
        public string Sinhala { get; init; } = "";
        public string Tamil { get; init; } = "";
        public string Mfd { get; init; } = "";
        public string Expiry { get; init; } = "";
        public string Batch { get; init; } = "";
        public string NetWeight { get; init; } = "";
        public string Sugar { get; init; } = "";
        public string Salt { get; init; } = "";
        public string Fat { get; init; } = "";
        public string Mrp { get; init; } = "";
        public string Importer { get; init; } = "";
        public int Copies { get; init; } = 1;
        public bool PrintMfd { get; init; }
        public bool PrintExpiry { get; init; }
        public bool PrintBatch { get; init; }
        public bool PrintNetWeight { get; init; }
    }
}
