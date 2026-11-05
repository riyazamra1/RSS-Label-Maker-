using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Printing;

namespace RSS.LabelMaker.App;

internal interface ITransliterationProvider
{
    string Id { get; }
    Task<string> TransliterateAsync(string source, string targetLanguage, CancellationToken cancellationToken = default);
}

internal sealed class GoogleInputToolsProvider : ITransliterationProvider
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };
    public string Id => "google-input-tools-v1";

    public async Task<string> TransliterateAsync(string source, string targetLanguage, CancellationToken cancellationToken = default)
    {
        var code = targetLanguage == "si" ? "si-t-i0-und" : targetLanguage == "ta" ? "ta-t-i0-und" : throw new ArgumentOutOfRangeException(nameof(targetLanguage));
        var url = "https://inputtools.google.com/request?text=" + Uri.EscapeDataString(source) +
                  "&itc=" + code + "&num=5&cp=0&cs=1&ie=utf-8&oe=utf-8";
        using var response = await Client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() < 2 ||
            root[0].GetString() != "SUCCESS" || root[1].GetArrayLength() == 0)
            throw new InvalidOperationException("The transliteration provider did not return a successful result.");
        var alternatives = root[1][0][1];
        if (alternatives.ValueKind != JsonValueKind.Array || alternatives.GetArrayLength() == 0)
            throw new InvalidOperationException("No transliteration suggestion was returned.");
        var result = alternatives[0].GetString()?.Trim();
        return string.IsNullOrWhiteSpace(result)
            ? throw new InvalidOperationException("The transliteration provider returned an empty result.")
            : result;
    }
}

internal sealed class LabelDesignDocument
{
    public double WidthMm { get; set; } = 70;
    public double HeightMm { get; set; } = 45;
    public List<LabelDesignElement> Elements { get; set; } = new();
}
internal sealed class LabelDesignElement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = "text";
    public string Text { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 55;
    public double Height { get; set; } = 8;
    public double FontSize { get; set; } = 10;
    public string FontFamily { get; set; } = "Arial";
    public string Color { get; set; } = "#20242B";
    public bool Bold { get; set; }
    public string Alignment { get; set; } = "Left";
    public string ImageBase64 { get; set; } = "";
}

public partial class MainWindow
{
    private static readonly ITransliterationProvider TransliterationProvider = new GoogleInputToolsProvider();

    private void MainWindow_AdvancedLoaded(object sender, RoutedEventArgs e)
    {
        LoadNutritionUnits();
        Sugar.TextChanged += (_, _) => UpdateNutritionUnitPreview();
        Salt.TextChanged += (_, _) => UpdateNutritionUnitPreview();
        Fat.TextChanged += (_, _) => UpdateNutritionUnitPreview();
        UpdateNutritionUnitPreview();
    }

    private void LoadNutritionUnits()
    {
        try
        {
            var path = NutritionUnitsPath();
            if (!File.Exists(path)) return;
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
            SetUnit(SugarUnit, data, "Sugar");
            SetUnit(SaltUnit, data, "Salt");
            SetUnit(FatUnit, data, "Fat");
        }
        catch { }
    }

    private static void SetUnit(ComboBox control, Dictionary<string, string>? values, string key)
    {
        if (values is null || !values.TryGetValue(key, out var value)) return;
        foreach (ComboBoxItem item in control.Items)
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                control.SelectedItem = item;
    }

    private static string NutritionUnitsPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Razeen Secure Solution", "RSS Label Maker", "nutrition-units.json");

    private void NutritionUnit_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(NutritionUnitsPath())!);
            var values = new Dictionary<string, string>
            {
                ["Sugar"] = SugarUnit.SelectedItem is ComboBoxItem s ? s.Content?.ToString() ?? "g/100g" : "g/100g",
                ["Salt"] = SaltUnit.SelectedItem is ComboBoxItem a ? a.Content?.ToString() ?? "g/100g" : "g/100g",
                ["Fat"] = FatUnit.SelectedItem is ComboBoxItem f ? f.Content?.ToString() ?? "g/100g" : "g/100g"
            };
            File.WriteAllText(NutritionUnitsPath(), JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true }));
            UpdateNutritionUnitPreview();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Nutrition unit save: " + ex.Message); }
    }

    private void UpdateNutritionUnitPreview()
    {
        if (!IsInitialized) return;
        var su = (SugarUnit.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "g/100g";
        var sa = (SaltUnit.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "g/100g";
        var fa = (FatUnit.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "g/100g";
        PreviewSugar.Text = $"{(string.IsNullOrWhiteSpace(Sugar.Text) ? "—" : Sugar.Text)} {su}";
        PreviewSalt.Text = $"{(string.IsNullOrWhiteSpace(Salt.Text) ? "—" : Salt.Text)} {sa}";
        PreviewFat.Text = $"{(string.IsNullOrWhiteSpace(Fat.Text) ? "—" : Fat.Text)} {fa}";
    }

    private async void TransliterateBoth_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireVerifiedEmail()) return;
        var source = EnglishName.Text.Trim();
        if (source.Length == 0) { ProductStatus.Text = "Enter the English product name first."; return; }
        if (sender is not Button button) return;
        button.IsEnabled = false;
        try
        {
            ProductStatus.Text = "Generating Sinhala and Tamil transliterations…";
            var si = await TransliterateAsync(source, "si", _accountSession!);
            var ta = await TransliterateAsync(source, "ta", _accountSession!);
            if (string.IsNullOrWhiteSpace(SinhalaName.Text)) SinhalaName.Text = si;
            else if (MessageBox.Show(this, "Replace the manually edited Sinhala name?", "Confirm transliteration", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) SinhalaName.Text = si;
            if (string.IsNullOrWhiteSpace(TamilName.Text)) TamilName.Text = ta;
            else if (MessageBox.Show(this, "Replace the manually edited Tamil name?", "Confirm transliteration", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) TamilName.Text = ta;
            ProductStatus.Text = "Sinhala and Tamil transliterations are ready. Review and edit them before saving.";
        }
        catch (Exception ex)
        {
            ProductStatus.Text = "Transliteration was not completed: " + ex.Message;
            MessageBox.Show(this, ex.Message, "RSS Transliteration", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally { button.IsEnabled = true; }
    }

    private async Task<string> TransliterateAsync(string source, string target, RssAccountSession session)
    {
        var accountKey = session.Id;
        using var connection = new SqliteConnection($"Data Source={GetDatabasePath()}");
        connection.Open();
        using (var schema = connection.CreateCommand())
        {
            schema.CommandText = """
                CREATE TABLE IF NOT EXISTS TransliterationCache(
                    AccountId TEXT NOT NULL, SourceText TEXT NOT NULL, TargetLanguage TEXT NOT NULL,
                    ResultText TEXT NOT NULL, Provider TEXT NOT NULL, CreatedUtc TEXT NOT NULL,
                    PRIMARY KEY(AccountId, SourceText, TargetLanguage, Provider));
                CREATE TABLE IF NOT EXISTS TransliterationUsage(
                    AccountId TEXT PRIMARY KEY, SuccessfulUses INTEGER NOT NULL DEFAULT 0);
                """;
            schema.ExecuteNonQuery();
        }
        using (var cached = connection.CreateCommand())
        {
            cached.CommandText = "SELECT ResultText FROM TransliterationCache WHERE AccountId=$a AND SourceText=$s AND TargetLanguage=$t AND Provider=$p;";
            cached.Parameters.AddWithValue("$p", TransliterationProvider.Id);
            cached.Parameters.AddWithValue("$a", accountKey);
            cached.Parameters.AddWithValue("$s", source);
            cached.Parameters.AddWithValue("$t", target);
            var value = Convert.ToString(cached.ExecuteScalar(), CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        var premium = session.Plan.Contains("premium", StringComparison.OrdinalIgnoreCase) ||
                      session.Plan.Contains("trial", StringComparison.OrdinalIgnoreCase);
        int uses;
        using (var usage = connection.CreateCommand())
        {
            usage.CommandText = "SELECT SuccessfulUses FROM TransliterationUsage WHERE AccountId=$a;";
            usage.Parameters.AddWithValue("$a", accountKey);
            uses = Convert.ToInt32(usage.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
        }
        if (uses >= 5 && !premium)
            throw new InvalidOperationException("Your five free automatic transliteration uses have been used. An active Premium subscription is required for further automatic transliteration.");
        var result = await TransliterationProvider.TransliterateAsync(source, target);
        using var transaction = connection.BeginTransaction();
        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT OR REPLACE INTO TransliterationCache(AccountId,SourceText,TargetLanguage,ResultText,Provider,CreatedUtc) VALUES($a,$s,$t,$r,$p,$d);";
            insert.Parameters.AddWithValue("$a", accountKey);
            insert.Parameters.AddWithValue("$s", source);
            insert.Parameters.AddWithValue("$t", target);
            insert.Parameters.AddWithValue("$r", result);
            insert.Parameters.AddWithValue("$p", TransliterationProvider.Id);
            insert.Parameters.AddWithValue("$d", DateTimeOffset.UtcNow.ToString("O"));
            insert.ExecuteNonQuery();
        }
        using (var count = connection.CreateCommand())
        {
            count.Transaction = transaction;
            count.CommandText = "INSERT INTO TransliterationUsage(AccountId,SuccessfulUses) VALUES($a,1) ON CONFLICT(AccountId) DO UPDATE SET SuccessfulUses=SuccessfulUses+1;";
            count.Parameters.AddWithValue("$a", accountKey);
            count.ExecuteNonQuery();
        }
        transaction.Commit();
        return result;
    }

    private void OpenCanvasStudio_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireVerifiedEmail()) return;
        var document = LabelStudioWindow.LoadDocument(GetDatabasePath(),
            ParseMm(LabelWidthInput.Text, 70), ParseMm(LabelHeightInput.Text, 45));
        var values = BuildStudioValues();
        var studio = new LabelStudioWindow(GetDatabasePath(), document, values) { Owner = this };
        studio.ShowDialog();
    }

    private void PrintPreview_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireVerifiedEmail()) return;
        var document = LabelStudioWindow.LoadDocument(GetDatabasePath(),
            ParseMm(LabelWidthInput.Text, 70), ParseMm(LabelHeightInput.Text, 45));
        var values = BuildStudioValues();
        var width = LabelPaperSize.SelectedIndex switch { 1 => 148d, 2 => 215.9d, 3 => ParseMm(CustomPaperWidth.Text, 210), _ => 210d };
        var height = LabelPaperSize.SelectedIndex switch { 1 => 210d, 2 => 279.4d, 3 => ParseMm(CustomPaperHeight.Text, 297), _ => 297d };
        var preview = new LabelPrintPreviewWindow(document, values, width, height,
            ParseMm(MarginTopInput.Text, 5), ParseMm(MarginRightInput.Text, 5),
            ParseMm(MarginBottomInput.Text, 5), ParseMm(MarginLeftInput.Text, 5),
            ParseMm(GapHorizontalInput.Text, 3), ParseMm(GapVerticalInput.Text, 3),
            Math.Max(1, int.TryParse(Copies.Text, out var copies) ? copies : 1));
        preview.Owner = this;
        preview.ShowDialog();
    }

    private Dictionary<string, string> BuildStudioValues()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (ProductsGrid.SelectedItem is ProductRow selected)
        {
            foreach (var name in new[] { "English", "Sinhala", "Tamil", "Batch", "NetWeight", "Sugar", "Salt", "Fat", "Mrp", "Importer" })
                values[name] = Convert.ToString(typeof(ProductRow).GetProperty(name)?.GetValue(selected), CultureInfo.InvariantCulture) ?? "";
            values["Mfd"] = selected.PrintMfd ? "Printed on Pack" : selected.Mfd;
            values["Expiry"] = selected.PrintExpiry ? "Printed on Pack" : selected.Expiry;
            values["Batch"] = selected.PrintBatch ? "Printed on Pack" : selected.Batch;
            values["NetWeight"] = selected.PrintNetWeight ? "Printed on Pack" : selected.NetWeight;
        }
        values["SugarUnit"] = (SugarUnit.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "g/100g";
        values["SaltUnit"] = (SaltUnit.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "g/100g";
        values["FatUnit"] = (FatUnit.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "g/100g";
        return values;
    }

    private static double ParseMm(string value, double fallback) =>
        double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) &&
        double.IsFinite(parsed) && parsed > 0 ? parsed : fallback;
}

internal sealed class LabelStudioWindow : Window
{
    private const double DipPerMm = 96d / 25.4d;
    private readonly string _databasePath;
    private readonly LabelDesignDocument _document;
    private readonly Dictionary<string, string> _values;
    private readonly Canvas _canvas = new();
    private readonly ListBox _layers = new();
    private readonly TextBox _text = new();
    private readonly TextBox _x = new();
    private readonly TextBox _y = new();
    private readonly TextBox _width = new();
    private readonly TextBox _height = new();
    private readonly TextBox _fontSize = new();
    private readonly ComboBox _color = new();
    private readonly ComboBox _fontFamily = new();
    private readonly ComboBox _alignment = new();
    private readonly CheckBox _bold = new();
    private LabelDesignElement? _selected;
    private Point _dragOrigin;
    private double _elementX, _elementY;

    public LabelStudioWindow(string databasePath, LabelDesignDocument document, Dictionary<string, string> values)
    {
        _databasePath = databasePath;
        _document = document;
        _values = values;
        Title = "RSS Label Maker — Full Canvas Designer";
        Width = 1180; Height = 760; MinWidth = 900; MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(245, 246, 248));
        BuildUi();
        RenderCanvas();
    }

    public static LabelDesignDocument LoadDocument(string path, double width, double height)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using (var schema = connection.CreateCommand())
        {
            schema.CommandText = """
                CREATE TABLE IF NOT EXISTS LabelDesigns(
                    Id INTEGER PRIMARY KEY CHECK(Id=1), DataJson TEXT NOT NULL, UpdatedUtc TEXT NOT NULL);
                """;
            schema.ExecuteNonQuery();
        }
        using var read = connection.CreateCommand();
        read.CommandText = "SELECT DataJson FROM LabelDesigns WHERE Id=1;";
        var json = Convert.ToString(read.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var saved = JsonSerializer.Deserialize<LabelDesignDocument>(json);
                if (saved is not null && saved.Elements.Count > 0) return saved;
            }
            catch { }
        }
        return CreateDefault(width, height);
    }

    private static LabelDesignDocument CreateDefault(double width, double height)
    {
        var d = new LabelDesignDocument { WidthMm = width, HeightMm = height };
        d.Elements.Add(new() { Text = "{{English}}", X = 2, Y = 1, Width = width - 4, Height = 7, FontSize = 16, Color = "#C83232", Bold = true, Alignment = "Center" });
        d.Elements.Add(new() { Text = "{{Sinhala}}  •  {{Tamil}}", X = 2, Y = 8, Width = width - 4, Height = 5, FontSize = 9, Color = "#C83232", Alignment = "Center" });
        d.Elements.Add(new() { Text = "M.F.D: {{Mfd}}     EXP.D: {{Expiry}}", X = 3, Y = 14, Width = width - 6, Height = 5, FontSize = 7 });
        d.Elements.Add(new() { Text = "B.No: {{Batch}}     N.Wt: {{NetWeight}}", X = 3, Y = 20, Width = width - 6, Height = 5, FontSize = 7 });
        d.Elements.Add(new() { Text = "M.R.P. Rs. {{Mrp}}", X = 2, Y = 26, Width = width - 4, Height = 6, FontSize = 12, Bold = true, Alignment = "Center" });
        d.Elements.Add(new() { Text = "SUGAR {{Sugar}} {{SugarUnit}}   SALT {{Salt}} {{SaltUnit}}   FAT {{Fat}} {{FatUnit}}", X = 2, Y = 33, Width = width - 4, Height = 6, FontSize = 6 });
        d.Elements.Add(new() { Text = "Import By: {{Importer}}", X = 2, Y = 40, Width = width - 4, Height = 4, FontSize = 6 });
        return d;
    }

    private void BuildUi()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var tools = new StackPanel { Margin = new Thickness(16), Background = Brushes.White };
        tools.Children.Add(new TextBlock { Text = "CANVAS ELEMENTS", FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 12) });
        AddButton(tools, "Add text", () => AddElement("New text", "text"));
        AddButton(tools, "Add product field", () => AddElement("{{English}}", "text"));
        AddButton(tools, "Add rectangle", () => AddElement("", "shape"));
        AddButton(tools, "Add image", AddImage);
        AddButton(tools, "Duplicate selected", DuplicateSelected);
        AddButton(tools, "Delete selected", DeleteSelected);
        tools.Children.Add(new Separator { Margin = new Thickness(0, 12, 0, 8) });
        tools.Children.Add(new TextBlock { Text = "SELECTED ELEMENT", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
        AddField(tools, "Text / field token", _text);
        AddField(tools, "X position (mm)", _x);
        AddField(tools, "Y position (mm)", _y);
        AddField(tools, "Width (mm)", _width);
        AddField(tools, "Height (mm)", _height);
        AddField(tools, "Font size (pt)", _fontSize);
        _fontFamily.ItemsSource = new[] { "Arial", "Calibri", "Segoe UI", "Nirmala UI", "Iskoola Pota", "Latha" };
        tools.Children.Add(new TextBlock { Text = "Font family", FontSize = 10, Margin = new Thickness(0, 5, 0, 2) });
        tools.Children.Add(_fontFamily);
        _alignment.ItemsSource = new[] { "Left", "Center", "Right" };
        tools.Children.Add(new TextBlock { Text = "Text alignment", FontSize = 10, Margin = new Thickness(0, 5, 0, 2) });
        tools.Children.Add(_alignment);
        _color.ItemsSource = new[] { "#20242B", "#C83232", "#458A58", "#D99B37", "#FFFFFF", "#000000" };
        tools.Children.Add(new TextBlock { Text = "Text colour", FontSize = 10, Margin = new Thickness(0, 5, 0, 2) });
        tools.Children.Add(_color);
        _bold.Content = "Bold";
        tools.Children.Add(_bold);
        _text.TextChanged += (_, _) => { if (_selected is not null) { _selected.Text = _text.Text; RenderCanvas(false); } };
        _color.SelectionChanged += (_, _) => { if (_selected is not null && _color.SelectedItem is string c) { _selected.Color = c; RenderCanvas(false); } };
        _bold.Checked += (_, _) => { if (_selected is not null) { _selected.Bold = true; RenderCanvas(false); } };
        _bold.Unchecked += (_, _) => { if (_selected is not null) { _selected.Bold = false; RenderCanvas(false); } };
        _fontFamily.SelectionChanged += (_, _) => { if (_selected is not null && _fontFamily.SelectedItem is string family) { _selected.FontFamily = family; RenderCanvas(false); } };
        _alignment.SelectionChanged += (_, _) => { if (_selected is not null && _alignment.SelectedItem is string alignment) { _selected.Alignment = alignment; RenderCanvas(false); } };
        var apply = new Button { Content = "Apply position / size", Margin = new Thickness(0, 8, 0, 8), Padding = new Thickness(8) };
        apply.Click += (_, _) => ApplyGeometry();
        tools.Children.Add(apply);
        tools.Children.Add(new TextBlock { Text = "LAYERS", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 8, 0, 5) });
        _layers.Height = 130;
        _layers.SelectionChanged += (_, _) =>
        {
            if (_layers.SelectedItem is LabelDesignElement element) Select(element);
        };
        tools.Children.Add(_layers);
        AddButton(tools, "Save design", SaveDesign);
        AddButton(tools, "Preview and print", ShowPrintPreview);
        var scroller = new ScrollViewer { Content = _canvas, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = new SolidColorBrush(Color.FromRgb(232, 234, 238)) };
        _canvas.Width = _document.WidthMm * DipPerMm;
        _canvas.Height = _document.HeightMm * DipPerMm;
        _canvas.Background = Brushes.White;
        _canvas.ClipToBounds = true;
        _canvas.Margin = new Thickness(45);
        _canvas.MouseRightButtonDown += (_, _) => Select(null);
        Grid.SetColumn(scroller, 1);
        root.Children.Add(new ScrollViewer { Content = tools, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        root.Children.Add(scroller);
        Content = root;
    }

    private static void AddField(Panel panel, string label, TextBox input)
    {
        panel.Children.Add(new TextBlock { Text = label, FontSize = 10, Margin = new Thickness(0, 4, 0, 2) });
        input.Margin = new Thickness(0, 0, 0, 3);
        panel.Children.Add(input);
    }
    private static void AddButton(Panel panel, string label, Action action)
    {
        var button = new Button { Content = label, Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 2, 0, 2) };
        button.Click += (_, _) => action();
        panel.Children.Add(button);
    }

    private void AddImage()
    {
        var dialog = new OpenFileDialog { Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var element = new LabelDesignElement
            {
                Kind = "image", Text = "Image",
                ImageBase64 = Convert.ToBase64String(File.ReadAllBytes(dialog.FileName)),
                X = 3, Y = 3, Width = Math.Min(30, _document.WidthMm - 6), Height = 18
            };
            _document.Elements.Add(element);
            RenderCanvas();
            Select(element);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not add image", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void AddElement(string text, string kind)
    {
        var element = new LabelDesignElement
        {
            Kind = kind, Text = text, X = 3, Y = 3 + _document.Elements.Count * 7,
            Width = Math.Max(15, _document.WidthMm - 6), Height = kind == "shape" ? 12 : 7
        };
        _document.Elements.Add(element);
        RenderCanvas();
        Select(element);
    }
    private void DuplicateSelected()
    {
        if (_selected is null) return;
        var copy = JsonSerializer.Deserialize<LabelDesignElement>(JsonSerializer.Serialize(_selected))!;
        copy.Id = Guid.NewGuid(); copy.X += 2; copy.Y += 2;
        _document.Elements.Add(copy); RenderCanvas(); Select(copy);
    }
    private void DeleteSelected()
    {
        if (_selected is null) return;
        _document.Elements.Remove(_selected); _selected = null; RenderCanvas(); Select(null);
    }
    private void Select(LabelDesignElement? element)
    {
        _selected = element;
        _text.TextChanged -= SelectedTextChanged;
        _text.Text = element?.Text ?? "";
        _text.TextChanged += SelectedTextChanged;
        _x.Text = element?.X.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
        _y.Text = element?.Y.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
        _width.Text = element?.Width.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
        _height.Text = element?.Height.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
        _fontSize.Text = element?.FontSize.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
        _color.SelectedItem = element?.Color;
        _bold.IsChecked = element?.Bold == true;
        _fontFamily.SelectedItem = element?.FontFamily;
        _alignment.SelectedItem = element?.Alignment;
        _layers.SelectedItem = element;
    }
    private void SelectedTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_selected is not null) { _selected.Text = _text.Text; RenderCanvas(false); }
    }
    private void ApplyGeometry()
    {
        if (_selected is null) return;
        if (!Try(_x.Text, out var x) || !Try(_y.Text, out var y) || !Try(_width.Text, out var w) || !Try(_height.Text, out var h) || !Try(_fontSize.Text, out var fs) ||
            x < 0 || y < 0 || w <= 0 || h <= 0 || fs <= 0)
        { MessageBox.Show(this, "Enter valid non-negative positions and positive dimensions/font size.", "Invalid element geometry"); return; }
        _selected.X = x; _selected.Y = y; _selected.Width = w; _selected.Height = h; _selected.FontSize = fs;
        RenderCanvas(false);
    }
    private static bool Try(string s, out double value) => double.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    private void RenderCanvas(bool rebuildLayers = true)
    {
        _canvas.Children.Clear();
        if (rebuildLayers)
        {
            _layers.ItemsSource = null;
            _layers.ItemsSource = _document.Elements;
            _layers.DisplayMemberPath = "Text";
        }
        foreach (var element in _document.Elements)
        {
            FrameworkElement visual;
            if (element.Kind == "shape")
                visual = new Border { BorderBrush = BrushFrom(element.Color), BorderThickness = new Thickness(1), Background = Brushes.Transparent };
            else if (element.Kind == "image")
                visual = new Image { Source = LoadImage(element.ImageBase64), Stretch = Stretch.Uniform };
            else
                visual = new TextBlock
                {
                    Text = element.Text, FontSize = element.FontSize * 96d / 72d, FontFamily = new FontFamily(element.FontFamily),
                    Foreground = BrushFrom(element.Color), FontWeight = element.Bold ? FontWeights.Bold : FontWeights.Normal,
                    TextWrapping = TextWrapping.Wrap, TextAlignment = element.Alignment == "Center" ? TextAlignment.Center : element.Alignment == "Right" ? TextAlignment.Right : TextAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center
                };
            visual.Width = element.Width * DipPerMm;
            visual.Height = element.Height * DipPerMm;
            visual.Opacity = ReferenceEquals(element, _selected) ? 1 : 0.92;
            if (ReferenceEquals(element, _selected))
            {
                visual = new Border { BorderBrush = Brushes.DodgerBlue, BorderThickness = new Thickness(1), Child = visual };
                visual.Width = element.Width * DipPerMm;
                visual.Height = element.Height * DipPerMm;
            }
            Canvas.SetLeft(visual, element.X * DipPerMm);
            Canvas.SetTop(visual, element.Y * DipPerMm);
            visual.Cursor = Cursors.SizeAll;
            visual.MouseLeftButtonDown += (_, ev) =>
            {
                Select(element);
                _dragOrigin = ev.GetPosition(_canvas);
                _elementX = element.X; _elementY = element.Y;
                visual.CaptureMouse();
                ev.Handled = true;
            };
            visual.MouseMove += (_, ev) =>
            {
                if (visual.IsMouseCaptured && ev.LeftButton == MouseButtonState.Pressed)
                {
                    var p = ev.GetPosition(_canvas);
                    element.X = Math.Max(0, _elementX + (p.X - _dragOrigin.X) / DipPerMm);
                    element.Y = Math.Max(0, _elementY + (p.Y - _dragOrigin.Y) / DipPerMm);
                    Canvas.SetLeft(visual, element.X * DipPerMm);
                    Canvas.SetTop(visual, element.Y * DipPerMm);
                    _x.Text = element.X.ToString("0.##", CultureInfo.InvariantCulture);
                    _y.Text = element.Y.ToString("0.##", CultureInfo.InvariantCulture);
                }
            };
            visual.MouseLeftButtonUp += (_, _) => { if (visual.IsMouseCaptured) visual.ReleaseMouseCapture(); RenderCanvas(false); };
            _canvas.Children.Add(visual);
        }
        var overflow = _document.Elements.Count(e => e.X < 0 || e.Y < 0 || e.X + e.Width > _document.WidthMm || e.Y + e.Height > _document.HeightMm);
        Title = overflow == 0 ? "RSS Label Maker — Full Canvas Designer" : $"RSS Label Maker — Designer ({overflow} overflow warning(s))";
    }
    private static BitmapImage LoadImage(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        using var stream = new MemoryStream(bytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static Brush BrushFrom(string color)
    {
        try { return (Brush)new BrushConverter().ConvertFromString(color)!; }
        catch { return Brushes.Black; }
    }

    private void SaveDesign()
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS LabelDesigns(Id INTEGER PRIMARY KEY CHECK(Id=1),DataJson TEXT NOT NULL,UpdatedUtc TEXT NOT NULL);
                INSERT INTO LabelDesigns(Id,DataJson,UpdatedUtc) VALUES(1,$json,$date)
                ON CONFLICT(Id) DO UPDATE SET DataJson=excluded.DataJson,UpdatedUtc=excluded.UpdatedUtc;
                """;
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(_document));
            command.Parameters.AddWithValue("$date", DateTimeOffset.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
            MessageBox.Show(this, "The editable label design has been saved to this PC.", "RSS Label Maker", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not save design", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ShowPrintPreview()
    {
        var preview = new LabelPrintPreviewWindow(_document, _values, 210, 297, 5, 5, 5, 5, 3, 3, 1) { Owner = this };
        preview.ShowDialog();
    }
}

internal sealed class LabelPrintPreviewWindow : Window
{
    private const double DipPerMm = 96d / 25.4d;
    private readonly FixedDocument _fixedDocument = new();
    public LabelPrintPreviewWindow(LabelDesignDocument design, Dictionary<string, string> values,
        double paperWidthMm, double paperHeightMm, double top, double right, double bottom, double left,
        double gapX, double gapY, int copies)
    {
        Title = "RSS Label Maker — Actual-size Print Preview";
        Width = 1000; Height = 760; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var paperW = paperWidthMm * DipPerMm;
        var paperH = paperHeightMm * DipPerMm;
        var labelW = design.WidthMm * DipPerMm;
        var labelH = design.HeightMm * DipPerMm;
        var columns = Math.Max(0, (int)Math.Floor((paperWidthMm - left - right + gapX) / (design.WidthMm + gapX)));
        var rows = Math.Max(0, (int)Math.Floor((paperHeightMm - top - bottom + gapY) / (design.HeightMm + gapY)));
        if (columns < 1 || rows < 1) throw new InvalidOperationException("The label does not fit inside the configured paper margins.");
        var perPage = columns * rows;
        var pages = Math.Max(1, (int)Math.Ceiling(copies / (double)perPage));
        for (var pageIndex = 0; pageIndex < pages; pageIndex++)
        {
            var page = new FixedPage { Width = paperW, Height = paperH, Background = Brushes.White };
            var pageCanvas = new Canvas { Width = paperW, Height = paperH, Background = Brushes.White };
            var count = Math.Min(perPage, copies - pageIndex * perPage);
            for (var i = 0; i < count; i++)
            {
                var row = i / columns;
                var col = i % columns;
                var label = new Canvas { Width = labelW, Height = labelH, Background = Brushes.White, ClipToBounds = true };
                var border = new Border { Width = labelW, Height = labelH, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0.6) };
                label.Children.Add(border);
                foreach (var element in design.Elements)
                {
                    if (element.Kind == "shape")
                    {
                        var shape = new Border { Width = element.Width * DipPerMm, Height = element.Height * DipPerMm,
                            BorderBrush = Brushes.Black, BorderThickness = new Thickness(0.6), Background = Brushes.Transparent };
                        Canvas.SetLeft(shape, element.X * DipPerMm); Canvas.SetTop(shape, element.Y * DipPerMm);
                        label.Children.Add(shape);
                        continue;
                    }
                    if (element.Kind == "image")
                    {
                        var image = new Image { Source = LoadImage(element.ImageBase64), Width = element.Width * DipPerMm, Height = element.Height * DipPerMm, Stretch = Stretch.Uniform };
                        Canvas.SetLeft(image, element.X * DipPerMm);
                        Canvas.SetTop(image, element.Y * DipPerMm);
                        label.Children.Add(image);
                        continue;
                    }
                    var text = element.Text;
                    foreach (var pair in values) text = text.Replace("{{" + pair.Key + "}}", pair.Value ?? "", StringComparison.OrdinalIgnoreCase);
                    var block = new TextBlock
                    {
                        Text = text, Width = element.Width * DipPerMm, Height = element.Height * DipPerMm,
                        FontSize = element.FontSize * 96d / 72d, FontFamily = new FontFamily(element.FontFamily),
                        FontWeight = element.Bold ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = BrushFrom(element.Color), TextWrapping = TextWrapping.Wrap,
                        TextAlignment = element.Alignment == "Center" ? TextAlignment.Center : element.Alignment == "Right" ? TextAlignment.Right : TextAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Canvas.SetLeft(block, element.X * DipPerMm); Canvas.SetTop(block, element.Y * DipPerMm);
                    label.Children.Add(block);
                }
                Canvas.SetLeft(label, (left + col * (design.WidthMm + gapX)) * DipPerMm);
                Canvas.SetTop(label, (top + row * (design.HeightMm + gapY)) * DipPerMm);
                pageCanvas.Children.Add(label);
            }
            page.Children.Add(pageCanvas);
            var content = new PageContent();
            ((System.Windows.Markup.IAddChild)content).AddChild(page);
            _fixedDocument.Pages.Add(content);
        }
        _fixedDocument.DocumentPaginator.PageSize = new Size(paperW, paperH);
        var viewer = new DocumentViewer { Document = _fixedDocument, Margin = new Thickness(10) };
        var print = new Button { Content = "Print at 100% (Windows printer)", Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(10) };
        print.Click += (_, _) =>
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true) return;
            try { dialog.PrintTicket.PageMediaSize = new PageMediaSize(paperW, paperH); }
            catch { MessageBox.Show(this, "The selected printer driver may not support this custom page size. Check printer properties before printing.", "Paper size warning", MessageBoxButton.OK, MessageBoxImage.Warning); }
            try
            {
                var imageable = dialog.PrintQueue?.GetPrintCapabilities(dialog.PrintTicket).PageImageableArea;
                if (imageable is not null)
                {
                    var hardwareLeft = imageable.OriginWidth / DipPerMm;
                    var hardwareTop = imageable.OriginHeight / DipPerMm;
                    var hardwareRight = (paperW - imageable.OriginWidth - imageable.ExtentWidth) / DipPerMm;
                    var hardwareBottom = (paperH - imageable.OriginHeight - imageable.ExtentHeight) / DipPerMm;
                    if (left + 0.01 < hardwareLeft || top + 0.01 < hardwareTop || right + 0.01 < hardwareRight || bottom + 0.01 < hardwareBottom)
                    {
                        var warning = $"Configured margins (L{left:0.##}, T{top:0.##}, R{right:0.##}, B{bottom:0.##} mm) are smaller than this printer's reported non-printable margins (L{hardwareLeft:0.##}, T{hardwareTop:0.##}, R{hardwareRight:0.##}, B{hardwareBottom:0.##} mm). Output may be clipped. Continue?";
                        if (MessageBox.Show(this, warning, "Printer margin warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                    }
                }
            }
            catch { MessageBox.Show(this, "The printer driver did not provide printable-area information. Verify hardware margins and print a calibration page before production.", "Printer capability warning", MessageBoxButton.OK, MessageBoxImage.Warning); }
            dialog.PrintDocument(_fixedDocument.DocumentPaginator, "RSS Label Maker labels");
        };
        var root = new DockPanel();
        DockPanel.SetDock(print, Dock.Bottom);
        root.Children.Add(print);
        root.Children.Add(viewer);
        Content = root;
    }
    private static Brush BrushFrom(string color)
    {
        try { return (Brush)new BrushConverter().ConvertFromString(color)!; }
        catch { return Brushes.Black; }
    }
}