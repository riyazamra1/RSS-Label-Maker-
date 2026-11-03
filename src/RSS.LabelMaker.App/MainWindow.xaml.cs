using System.Collections.ObjectModel;
using System.Printing;
using System.Windows;
using System.Windows.Controls;

namespace RSS.LabelMaker.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ProductRow> _products = new();

    public MainWindow()
    {
        InitializeComponent();
        ProductsGrid.ItemsSource = _products;
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

    private sealed class ProductRow
    {
        public string English { get; init; } = "";
        public string Sinhala { get; init; } = "";
        public string Tamil { get; init; } = "";
        public string Batch { get; init; } = "";
        public string Mrp { get; init; } = "";
    }
}
