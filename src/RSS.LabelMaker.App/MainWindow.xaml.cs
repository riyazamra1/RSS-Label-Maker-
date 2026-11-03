using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace RSS.LabelMaker.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ProductRow> _products = new();

    public MainWindow()
    {
        InitializeComponent();
        ProductsGrid.ItemsSource = _products;
        WatermarkOpacitySlider.Value = 0.20;
        WatermarkOpacityValue.Text = "20%";
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

        _products.Add(new ProductRow
        {
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
            Mrp = Mrp.Text.Trim()
        });
        ProductCount.Text = _products.Count.ToString();
        ProductStatus.Text = "Added to the current session. Local database persistence is not implemented yet.";
        UpdatePreview();
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
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(Path.GetFullPath(dialog.FileName), UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            image.EndInit();
            image.Freeze();

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
    }

    private void RemoveWatermark_Click(object sender, RoutedEventArgs e)
    {
        PreviewWatermark.Source = null;
        PreviewWatermark.Visibility = Visibility.Collapsed;
        WatermarkFileName.Text = "No logo selected";
    }

    private void ProductField_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

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
        PrintStatus.Text = "Printer discovery works. Actual-size label rendering and test-page printing are scheduled for the printing milestone.";
    }

    private sealed class ProductRow
    {
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
    }
}
