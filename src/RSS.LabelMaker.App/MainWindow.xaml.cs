using System.Collections.ObjectModel;
using System.IO;
using System.Printing;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace RSS.LabelMaker.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ProductRow> _products = new();
    private readonly DispatcherTimer _clockTimer = new();
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

    public MainWindow()
    {
        InitializeComponent();
        ProductsGrid.ItemsSource = _products;
        RefreshPrinters();
        ShowPage("Dashboard");
        BuildClockAndSettingsUi();
        LoadAppSettings();
        WireDesignerOptionPersistence();
        UpdateClock();

        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        Closed += (_, _) => _clockTimer.Stop();
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
                    existingRightPanel.Margin = new Thickness(0, 0, 190, 0);

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

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string page)
            ShowPage(page);
    }

    private void ShowPage(string page)
    {
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
        if (string.IsNullOrWhiteSpace(EnglishName.Text))
        {
            ProductStatus.Text = "Enter an English product name before saving.";
            return;
        }

        _products.Add(new ProductRow
        {
            English = EnglishName.Text.Trim(),
            Sinhala = SinhalaName.Text.Trim(),
            Tamil = TamilName.Text.Trim(),
            Batch = Batch.Text.Trim(),
            Mrp = Mrp.Text.Trim()
        });
        ProductCount.Text = _products.Count.ToString();
        ProductStatus.Text = "Added to the current session. Local database persistence is part of the next milestone.";
        PreviewEnglish.Text = EnglishName.Text.Trim().ToUpperInvariant();
        PreviewLocal.Text = $"{SinhalaName.Text.Trim()}  •  {TamilName.Text.Trim()}".Trim(' ', '•');
    }

    private void ApplyLayout_Click(object sender, RoutedEventArgs e)
    {
        LayoutStatus.Text = "Dimension controls are shown in the prototype shell. Validated layout calculation and printer-area checks are next.";
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
    }

    private sealed class ProductRow
    {
        public string English { get; init; } = "";
        public string Sinhala { get; init; } = "";
        public string Tamil { get; init; } = "";
        public string Batch { get; init; } = "";
        public string Mrp { get; init; } = "";
    }
}
