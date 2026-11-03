using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Microsoft.Win32;

namespace RSS.LabelMaker.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ProductRow> _products = new();
    private WatermarkStore? _watermarkStore;
    private ProductStore? _productStore;
    private string? _editingProductId;

    public MainWindow()
    {
        InitializeComponent();
        ProductsGrid.ItemsSource = _products;
        try
        {
            _productStore = new ProductStore();
            foreach (var product in _productStore.Load())
                _products.Add(product);
            ProductCount.Text = _products.Count.ToString();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Local product storage could not be opened. Existing products cannot be loaded or saved until the database is available. {ex.Message}",
                "Product storage warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        WatermarkOpacitySlider.Value = 0.20;
        WatermarkOpacityValue.Text = "20%";
        try
        {
            _watermarkStore = new WatermarkStore();
            var savedWatermark = _watermarkStore.Load();
            WatermarkOpacitySlider.Value = savedWatermark.Opacity;
            if (savedWatermark.Image is { Length: > 0 } imageBytes)
            {
                PreviewWatermark.Source = DecodeWatermark(imageBytes);
                PreviewWatermark.Visibility = Visibility.Visible;
                WatermarkFileName.Text = "Saved watermark (local database)";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Local watermark storage could not be opened. The designer can still be used, but watermark changes will not persist. {ex.Message}",
                "Local storage warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        RefreshPrinters();
        ShowPage("Dashboard");
    }

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
            item.Background = selected ? System.Windows.Media.Brushes.DimGray : System.Windows.Media.Brushes.Transparent;
            item.Foreground = System.Windows.Media.Brushes.WhiteSmoke;
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
        if (_productStore is null)
        {
            ProductStatus.Text = "Product was not saved: local database is unavailable.";
            return;
        }

        if (ShowPerPiecePrice.IsChecked == true)
        {
            if (PerPiecePricingMode.SelectedIndex == 0)
            {
                if (!int.TryParse(PiecesPerBox.Text, NumberStyles.None, CultureInfo.CurrentCulture, out var pieces) ||
                    pieces <= 0 ||
                    !decimal.TryParse(BoxPrice.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var boxPrice) ||
                    boxPrice < 0)
                {
                    ProductStatus.Text = "Automatic per-piece pricing requires a positive whole-number Pieces per Box and a non-negative Box Price.";
                    return;
                }
            }
            else if (!decimal.TryParse(ManualPerPiecePrice.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var manualPrice) || manualPrice < 0)
            {
                ProductStatus.Text = "Enter a valid non-negative manual per-piece price.";
                return;
            }
        }

        var product = new ProductRow
        {
            Id = _editingProductId ?? Guid.NewGuid().ToString("N"),
            English = EnglishName.Text.Trim(),
            Sinhala = SinhalaName.Text.Trim(),
            Tamil = TamilName.Text.Trim(),
            Mfd = Mfd.Text.Trim(),
            Expiry = Expiry.Text.Trim(),
            Batch = Batch.Text.Trim(),
            NetWeight = NetWeight.Text.Trim(),
            MfdPrintOnPack = MfdPrintOnPack.IsChecked == true,
            ExpiryPrintOnPack = ExpiryPrintOnPack.IsChecked == true,
            BatchPrintOnPack = BatchPrintOnPack.IsChecked == true,
            NetWeightPrintOnPack = NetWeightPrintOnPack.IsChecked == true,
            Sugar = Sugar.Text.Trim(),
            Salt = Salt.Text.Trim(),
            Fat = Fat.Text.Trim(),
            Mrp = Mrp.Text.Trim(),
            Importer = Importer.Text.Trim(),
            PiecesPerBox = PiecesPerBox.Text.Trim(),
            BoxPrice = BoxPrice.Text.Trim(),
            ShowPerPiecePrice = ShowPerPiecePrice.IsChecked == true,
            PerPiecePricingMode = PerPiecePricingMode.SelectedIndex == 1 ? "Manual" : "Automatic",
            ManualPerPiecePrice = ManualPerPiecePrice.Text.Trim(),
            Copies = string.IsNullOrWhiteSpace(Copies.Text) ? "1" : Copies.Text.Trim()
        };

        try
        {
            if (_editingProductId is null)
            {
                _productStore.Save(product);
                _products.Add(product);
                ProductStatus.Text = "Product saved to the local database.";
            }
            else
            {
                _productStore.Update(product);
                var index = _products.ToList().FindIndex(item => item.Id == product.Id);
                if (index >= 0) _products[index] = product;
                _editingProductId = null;
                SaveProductButton.Content = "Save Product";
                ProductStatus.Text = "Product updated in the local database.";
            }
            ProductCount.Text = _products.Count.ToString();
            ProductsGrid.Items.Refresh();
            UpdatePreview();
        }
        catch (Exception ex)
        {
            ProductStatus.Text = $"Product could not be saved: {ex.Message}";
        }
    }

    private void EditProduct_Click(object sender, RoutedEventArgs e)
    {
        if (ProductsGrid.SelectedItem is not ProductRow product)
        {
            ProductStatus.Text = "Select a product from the list first.";
            return;
        }
        _editingProductId = product.Id;
        EnglishName.Text = product.English;
        SinhalaName.Text = product.Sinhala;
        TamilName.Text = product.Tamil;
        Mfd.Text = product.Mfd;
        Expiry.Text = product.Expiry;
        Batch.Text = product.Batch;
        NetWeight.Text = product.NetWeight;
        MfdPrintOnPack.IsChecked = product.MfdPrintOnPack;
        ExpiryPrintOnPack.IsChecked = product.ExpiryPrintOnPack;
        BatchPrintOnPack.IsChecked = product.BatchPrintOnPack;
        NetWeightPrintOnPack.IsChecked = product.NetWeightPrintOnPack;
        Sugar.Text = product.Sugar;
        Salt.Text = product.Salt;
        Fat.Text = product.Fat;
        Mrp.Text = product.Mrp;
        Importer.Text = product.Importer;
        PiecesPerBox.Text = product.PiecesPerBox;
        BoxPrice.Text = product.BoxPrice;
        ShowPerPiecePrice.IsChecked = product.ShowPerPiecePrice;
        PerPiecePricingMode.SelectedIndex = product.PerPiecePricingMode == "Manual" ? 1 : 0;
        ManualPerPiecePrice.Text = product.ManualPerPiecePrice;
        UpdatePerPiecePrice();
        Copies.Text = product.Copies;
        SaveProductButton.Content = "Update Product";
        ProductStatus.Text = $"Editing: {product.English}. Change the fields and click Update Product.";
        UpdatePreview();
    }

    private void DeleteProduct_Click(object sender, RoutedEventArgs e)
    {
        if (ProductsGrid.SelectedItem is not ProductRow product)
        {
            ProductStatus.Text = "Select a product from the list first.";
            return;
        }
        var answer = MessageBox.Show(this,
            $"Permanently delete '{product.English}' from this PC?",
            "Confirm product deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        if (_productStore is null)
        {
            ProductStatus.Text = "Product was not deleted: local database is unavailable.";
            return;
        }
        try
        {
            _productStore.Delete(product.Id);
            _products.Remove(product);
            if (_editingProductId == product.Id)
            {
                _editingProductId = null;
                SaveProductButton.Content = "Save Product";
            }
            ProductCount.Text = _products.Count.ToString();
            ProductStatus.Text = $"Deleted '{product.English}'.";
        }
        catch (Exception ex)
        {
            ProductStatus.Text = $"Product could not be deleted: {ex.Message}";
        }
    }

    private void ApplyLayout_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadMillimetres(LabelWidth.Text, out var labelWidth) ||
            !TryReadMillimetres(LabelHeight.Text, out var labelHeight) ||
            labelWidth <= 0 || labelHeight <= 0)
        {
            LayoutStatus.Text = "Enter valid positive label width and height in millimetres.";
            return;
        }

        var (paperWidth, paperHeight, paperName) = DesignerPaperSize.SelectedIndex switch
        {
            1 => (148m, 210m, "A5"),
            2 => (215.9m, 279.4m, "Letter"),
            _ => (210m, 297m, "A4")
        };

        var columns = (int)Math.Floor(paperWidth / labelWidth);
        var rows = (int)Math.Floor(paperHeight / labelHeight);
        var count = Math.Max(0, columns) * Math.Max(0, rows);

        if (columns < 1 || rows < 1)
        {
            LayoutStatus.Text = $"A {labelWidth:0.##} × {labelHeight:0.##} mm label does not fit on nominal {paperName} paper.";
            return;
        }

        var scale = Math.Min(7d, Math.Min(490d / (double)labelWidth, 315d / (double)labelHeight));
        PreviewLabelBorder.Width = (double)labelWidth * scale;
        PreviewLabelBorder.Height = (double)labelHeight * scale;
        LayoutStatus.Text = $"{paperName}: {columns} column(s) × {rows} row(s) = {count} label(s) per sheet. Nominal-paper estimate only; printer margins and gaps are not included.";
    }

    private static bool TryReadMillimetres(string value, out decimal result) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result) ||
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);

    private void ChooseWatermark_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a logo for the label watermark",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var imageBytes = File.ReadAllBytes(dialog.FileName);
            var image = DecodeWatermark(imageBytes);

            // Save the image bytes, not the original path: moving or deleting the source
            // file will not break the saved watermark.
            _watermarkStore?.SaveImage(imageBytes);
            PreviewWatermark.Source = image;
            PreviewWatermark.Visibility = Visibility.Visible;
            PreviewWatermark.Opacity = WatermarkOpacitySlider.Value;
            WatermarkFileName.Text = Path.GetFileName(dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"The selected image could not be loaded. {ex.Message}",
                "Watermark image error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void WatermarkOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (WatermarkOpacityValue is null || PreviewWatermark is null)
            return;

        var opacity = Math.Clamp(e.NewValue, 0d, 1d);
        PreviewWatermark.Opacity = opacity;
        WatermarkOpacityValue.Text = $"{Math.Round(opacity * 100):0}%";
        try
        {
            _watermarkStore?.SaveOpacity(opacity);
        }
        catch (Exception ex)
        {
            ProductStatus.Text = $"Watermark opacity changed in preview but could not be saved: {ex.Message}";
        }
    }

    private void RemoveWatermark_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _watermarkStore?.ClearImage();
            PreviewWatermark.Source = null;
            PreviewWatermark.Visibility = Visibility.Collapsed;
            WatermarkFileName.Text = "No logo selected";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"The watermark could not be removed from local storage. {ex.Message}",
                "Watermark storage error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static BitmapImage DecodeWatermark(byte[] imageBytes)
    {
        using var stream = new MemoryStream(imageBytes, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = stream;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void ProductField_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePerPiecePrice();
        UpdatePreview();
    }

    private void PerPiecePricing_Changed(object sender, RoutedEventArgs e) => UpdatePerPiecePrice();

    private void PerPiecePricingMode_Changed(object sender, SelectionChangedEventArgs e) => UpdatePerPiecePrice();

    private void UpdatePerPiecePrice()
    {
        if (PerPiecePricePanel is null || ShowPerPiecePrice is null)
            return;

        var enabled = ShowPerPiecePrice.IsChecked == true;
        PerPiecePricePanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        if (PerPiecePricingMode is null || ManualPerPiecePrice is null || CalculatedPerPiecePrice is null)
            return;

        var manual = PerPiecePricingMode.SelectedIndex == 1;
        ManualPerPiecePrice.Visibility = manual ? Visibility.Visible : Visibility.Collapsed;
        CalculatedPerPiecePrice.Visibility = manual ? Visibility.Collapsed : Visibility.Visible;

        if (!manual &&
            decimal.TryParse(BoxPrice.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var boxPrice) &&
            int.TryParse(PiecesPerBox.Text, NumberStyles.None, CultureInfo.CurrentCulture, out var pieces) &&
            pieces > 0 && boxPrice >= 0)
        {
            CalculatedPerPiecePrice.Text = (boxPrice / pieces).ToString("0.00", CultureInfo.CurrentCulture);
        }
        else if (!manual)
        {
            CalculatedPerPiecePrice.Text = "Enter a valid box price and pieces per box.";
        }
    }

    private void PrintOnPack_Changed(object sender, RoutedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        if (PreviewEnglish is null || EnglishName is null)
            return;

        PreviewEnglish.Text = string.IsNullOrWhiteSpace(EnglishName.Text)
            ? "PRODUCT NAME"
            : EnglishName.Text.Trim().ToUpperInvariant();
        PreviewLocal.Text = $"{SinhalaName.Text.Trim()}  •  {TamilName.Text.Trim()}".Trim(' ', '•');
        PreviewMfd.Text = FormatPreviewRow("M.F.D", Mfd.Text, MfdPrintOnPack.IsChecked == true);
        PreviewExpiry.Text = FormatPreviewRow("EXP.D", Expiry.Text, ExpiryPrintOnPack.IsChecked == true);
        PreviewBatch.Text = FormatPreviewRow("B.No", Batch.Text, BatchPrintOnPack.IsChecked == true);
        PreviewNetWeight.Text = FormatPreviewRow("N.Wt", NetWeight.Text, NetWeightPrintOnPack.IsChecked == true);
        PreviewMrp.Text = $"M.R.P. (Rs.)  {(string.IsNullOrWhiteSpace(Mrp.Text) ? "—" : Mrp.Text.Trim())}";
        if (PreviewPerPiece is not null)
        {
            var perPiece = GetPerPiecePriceText();
            PreviewPerPiece.Text = ShowPerPiecePrice.IsChecked == true && perPiece.Length > 0
                ? $"Per Piece (Rs.)  {perPiece}"
                : "";
            PreviewPerPiece.Visibility = PreviewPerPiece.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        PreviewSugar.Text = $"{DisplayNutrition(Sugar.Text)} g/100g";
        PreviewSalt.Text = $"{DisplayNutrition(Salt.Text)} g/100g";
        PreviewFat.Text = $"{DisplayNutrition(Fat.Text)} g/100g";
    }

    private static string FormatPreviewRow(string label, string value, bool printOnPack)
    {
        var display = printOnPack
            ? "Printed on Pack"
            : string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
        return $"{label} :  {display}";
    }

    private string GetPerPiecePriceText()
    {
        if (PerPiecePricingMode.SelectedIndex == 1)
            return ManualPerPiecePrice.Text.Trim();

        if (decimal.TryParse(BoxPrice.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var boxPrice) &&
            int.TryParse(PiecesPerBox.Text, NumberStyles.None, CultureInfo.CurrentCulture, out var pieces) &&
            pieces > 0 && boxPrice >= 0)
            return (boxPrice / pieces).ToString("0.00", CultureInfo.CurrentCulture);

        return "";
    }

    private static string DisplayNutrition(string value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

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
        try
        {
            using var server = new LocalPrintServer();
            var dialog = new PrintDialog();

            if (PrinterList.SelectedItem is string selectedPrinter &&
                !string.IsNullOrWhiteSpace(selectedPrinter))
            {
                dialog.PrintQueue = server.GetPrintQueue(selectedPrinter);
            }

            if (dialog.ShowDialog() != true)
            {
                PrintStatus.Text = "Calibration print cancelled.";
                return;
            }

            const double millimetresToDips = 96d / 25.4d;
            const double squareSize = 100d * millimetresToDips;
            const double inset = 5d * millimetresToDips;

            var calibrationVisual = new DrawingVisual();
            using (var drawing = calibrationVisual.RenderOpen())
            {
                var borderPen = new Pen(Brushes.Black, 0.8);
                var guidePen = new Pen(Brushes.Gray, 0.5);
                drawing.DrawRectangle(Brushes.White, borderPen,
                    new Rect(0, 0, squareSize, squareSize));

                // A measured 100 mm square with 50 mm centre guides.
                drawing.DrawLine(guidePen,
                    new Point(squareSize / 2, inset),
                    new Point(squareSize / 2, squareSize - inset));
                drawing.DrawLine(guidePen,
                    new Point(inset, squareSize / 2),
                    new Point(squareSize - inset, squareSize / 2));

                var title = new FormattedText(
                    "RSS LABEL MAKER - PRINTER CALIBRATION",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    9,
                    Brushes.Black,
                    1.0);
                drawing.DrawText(title, new Point(inset, inset));

                var dimension = new FormattedText(
                    "Measure outer border: 100 mm × 100 mm",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    8,
                    Brushes.Black,
                    1.0);
                drawing.DrawText(dimension,
                    new Point(inset, squareSize - inset - dimension.Height));

                var centreMark = new FormattedText(
                    "50 mm",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    8,
                    Brushes.Black,
                    1.0);
                drawing.DrawText(centreMark,
                    new Point(squareSize / 2 + 3, squareSize / 2 + 3));
            }

            dialog.PrintVisual(calibrationVisual, "RSS Label Maker - 100 mm calibration");
            PrintStatus.Text = "Calibration page sent to the selected printer. Measure the outer square; it should be 100 mm on each side. Driver scaling may affect the result.";
        }
        catch (Exception ex)
        {
            PrintStatus.Text = $"Calibration page could not be printed: {ex.Message}";
        }
    }

    private sealed class ProductRow
    {
        public ProductRow() { }

        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string English { get; init; } = "";
        public string Sinhala { get; init; } = "";
        public string Tamil { get; init; } = "";
        public string Mfd { get; init; } = "";
        public string Expiry { get; init; } = "";
        public string Batch { get; init; } = "";
        public string NetWeight { get; init; } = "";
        public bool MfdPrintOnPack { get; init; }
        public bool ExpiryPrintOnPack { get; init; }
        public bool BatchPrintOnPack { get; init; }
        public bool NetWeightPrintOnPack { get; init; }
        public string Sugar { get; init; } = "";
        public string Salt { get; init; } = "";
        public string Fat { get; init; } = "";
        public string Mrp { get; init; } = "";
        public string Importer { get; init; } = "";
        public string PiecesPerBox { get; init; } = "";
        public string BoxPrice { get; init; } = "";
        public bool ShowPerPiecePrice { get; init; }
        public string PerPiecePricingMode { get; init; } = "Automatic";
        public string ManualPerPiecePrice { get; init; } = "";
        public string Copies { get; init; } = "1";
    }

    private sealed class ProductStore
    {
        private readonly string _connectionString;

        public ProductStore()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Razeen Secure Solution", "RSS Label Maker");
            Directory.CreateDirectory(folder);
            _connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(folder, "rss-label-maker.db"),
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate
            }.ToString();

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS Products (
                    Id TEXT NOT NULL PRIMARY KEY,
                    DataJson TEXT NOT NULL,
                    CreatedUtc TEXT NOT NULL
                );
                """;
            command.ExecuteNonQuery();
        }

        public List<ProductRow> Load()
        {
            var products = new List<ProductRow>();
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT DataJson FROM Products ORDER BY CreatedUtc, Id;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var json = reader.GetString(0);
                var product = System.Text.Json.JsonSerializer.Deserialize<ProductRow>(json);
                if (product is not null)
                    products.Add(product);
            }
            return products;
        }

        public void Save(ProductRow product)
        {
            ArgumentNullException.ThrowIfNull(product);
            var json = System.Text.Json.JsonSerializer.Serialize(product);
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Products (Id, DataJson, CreatedUtc)
                VALUES ($id, $json, $createdUtc);
                """;
            command.Parameters.AddWithValue("$id", product.Id);
            command.Parameters.AddWithValue("$json", json);
            command.Parameters.AddWithValue("$createdUtc", DateTime.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
        }

        public void Update(ProductRow product)
        {
            ArgumentNullException.ThrowIfNull(product);
            var json = System.Text.Json.JsonSerializer.Serialize(product);
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Products SET DataJson = $json WHERE Id = $id;";
            command.Parameters.AddWithValue("$id", product.Id);
            command.Parameters.AddWithValue("$json", json);
            if (command.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("The product record no longer exists in the local database.");
        }

        public void Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A product ID is required.", nameof(id));
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Products WHERE Id = $id;";
            command.Parameters.AddWithValue("$id", id);
            if (command.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("The product record no longer exists in the local database.");
        }

        private Microsoft.Data.Sqlite.SqliteConnection OpenConnection()
        {
            var connection = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
            connection.Open();
            return connection;
        }
    }
}
