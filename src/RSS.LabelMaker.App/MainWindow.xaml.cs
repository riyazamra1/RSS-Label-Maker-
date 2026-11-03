using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Mail;
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
    private readonly ObservableCollection<CompanyProfileRow> _companyProfiles = new();
    private CompanyProfileStore? _companyProfileStore;
    private string? _editingCompanyProfileId;
    private WatermarkStore? _watermarkStore;
    private ProductStore? _productStore;
    private string? _editingProductId;

    public MainWindow()
    {
        InitializeComponent();
        ProductsGrid.ItemsSource = _products;
        PrintProductsList.ItemsSource = _products;
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
        try
        {
            _companyProfileStore = new CompanyProfileStore();
            foreach (var profile in _companyProfileStore.Load()) _companyProfiles.Add(profile);
            CompanyProfilesGrid.ItemsSource = _companyProfiles;
            CompanyProfileCount.Text = _companyProfiles.Count.ToString();
            RefreshProductProfileChoices();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Company profile storage could not be opened. {ex.Message}",
                "Company profile storage warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        RefreshPrinters();
        ShowPage("Dashboard");
    }


    private async void SignOut_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Sign out from RSS Label Maker on this PC? Local offline access will be removed.",
            "Confirm sign out", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        var token = DeviceCredentialStore.Read();
        if (!string.IsNullOrWhiteSpace(token))
        {
            try { await new AuthApiClient().PostAsync("/auth/logout", new { }, token); }
            catch { /* Local sign-out must still work when offline. */ }
        }
        DeviceCredentialStore.Clear();
        MessageBox.Show(this, "This PC has been signed out. Reopen RSS Label Maker to sign in again.",
            "Signed out", MessageBoxButton.OK, MessageBoxImage.Information);
        Application.Current.Shutdown();
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
        AboutPage.Visibility = page == "About" ? Visibility.Visible : Visibility.Collapsed;
        ContactPage.Visibility = page == "Contact" ? Visibility.Visible : Visibility.Collapsed;
        CompanyProfilesPage.Visibility = page == "CompanyProfiles" ? Visibility.Visible : Visibility.Collapsed;

        PageTitle.Text = page switch
        {
            "Designer" => "Label Designer",
            "Products" => "Products",
            "Templates" => "Templates",
            "Print" => "Print Manager",
            "Import" => "Import / Export",
            "CompanyProfiles" => "Import By & Distributed By",
            "About" => "About Razeen Secure Solution",
            "Contact" => "Contact RSS",
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
            "CompanyProfiles" => "Save reusable company and person details for product labels",
            "About" => "Company information and services",
            "Contact" => "Send an enquiry to Razeen Secure Solution",
            "Settings" => "Configure this local application",
            _ => "Your label production workspace"
        };
        var buttons = new[] { NavDashboard, NavProducts, NavDesigner, NavTemplates, NavPrint, NavImport, NavCompanyProfiles, NavAbout, NavContact, NavSettings };
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
            Importer = ImportByName.Text.Trim(),
            ImportByProfileId = ImportByProfile.SelectedValue?.ToString() ?? "",
            ImportByName = ImportByName.Text.Trim(),
            ImportByAddress = ImportByAddress.Text.Trim(),
            DistributedByProfileId = DistributedByProfile.SelectedValue?.ToString() ?? "",
            DistributedByName = DistributedByName.Text.Trim(),
            DistributedByAddress = DistributedByAddress.Text.Trim(),
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
        ImportByProfile.SelectedValue = product.ImportByProfileId;
        DistributedByProfile.SelectedValue = product.DistributedByProfileId;
        ImportByName.Text = string.IsNullOrWhiteSpace(product.ImportByName) ? product.Importer : product.ImportByName;
        ImportByAddress.Text = product.ImportByAddress;
        DistributedByName.Text = product.DistributedByName;
        DistributedByAddress.Text = product.DistributedByAddress;
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


    private void RefreshProductProfileChoices()
    {
        var importSelection = ImportByProfile?.SelectedValue?.ToString();
        var distributeSelection = DistributedByProfile?.SelectedValue?.ToString();
        ImportByProfile.ItemsSource = _companyProfiles.Where(p => p.ProfileType == "Import By").ToList();
        DistributedByProfile.ItemsSource = _companyProfiles.Where(p => p.ProfileType == "Distributed By").ToList();
        ImportByProfile.SelectedValue = importSelection;
        DistributedByProfile.SelectedValue = distributeSelection;
    }

    private void ImportByProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ImportByProfile.SelectedItem is CompanyProfileRow profile)
        {
            ImportByName.Text = profile.Name;
            ImportByAddress.Text = profile.Address;
        }
    }

    private void DistributedByProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DistributedByProfile.SelectedItem is CompanyProfileRow profile)
        {
            DistributedByName.Text = profile.Name;
            DistributedByAddress.Text = profile.Address;
        }
    }

    private void SaveCompanyProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_companyProfileStore is null) { CompanyProfileStatus.Text = "Company profile storage is unavailable."; return; }
        var name = CompanyProfileName.Text.Trim();
        var address = CompanyProfileAddress.Text.Trim();
        if (name.Length == 0 || address.Length == 0)
        { CompanyProfileStatus.Text = "Enter both a company/person name and the full address."; return; }
        var profile = new CompanyProfileRow
        {
            Id = _editingCompanyProfileId ?? Guid.NewGuid().ToString("N"),
            ProfileType = CompanyProfileType.SelectedIndex == 1 ? "Distributed By" : "Import By",
            Name = name, Address = address, Contact = CompanyProfileContact.Text.Trim()
        };
        try
        {
            if (_editingCompanyProfileId is null)
            { _companyProfileStore.Save(profile); _companyProfiles.Add(profile); CompanyProfileStatus.Text = "Company/person profile saved."; }
            else
            {
                _companyProfileStore.Update(profile);
                var index = _companyProfiles.ToList().FindIndex(item => item.Id == profile.Id);
                if (index >= 0) _companyProfiles[index] = profile;
                _editingCompanyProfileId = null; SaveCompanyProfileButton.Content = "Save Profile";
                CompanyProfileStatus.Text = "Company/person profile updated.";
            }
            CompanyProfileCount.Text = _companyProfiles.Count.ToString();
            CompanyProfilesGrid.Items.Refresh();
            RefreshProductProfileChoices();
        }
        catch (Exception ex) { CompanyProfileStatus.Text = $"Profile could not be saved: {ex.Message}"; }
    }

    private void EditCompanyProfile_Click(object sender, RoutedEventArgs e)
    {
        if (CompanyProfilesGrid.SelectedItem is not CompanyProfileRow profile)
        { CompanyProfileStatus.Text = "Select a profile first."; return; }
        _editingCompanyProfileId = profile.Id;
        CompanyProfileType.SelectedIndex = profile.ProfileType == "Distributed By" ? 1 : 0;
        CompanyProfileName.Text = profile.Name; CompanyProfileAddress.Text = profile.Address;
        CompanyProfileContact.Text = profile.Contact; SaveCompanyProfileButton.Content = "Update Profile";
        CompanyProfileStatus.Text = $"Editing {profile.Name}. Update the details and save.";
    }

    private void DeleteCompanyProfile_Click(object sender, RoutedEventArgs e)
    {
        if (CompanyProfilesGrid.SelectedItem is not CompanyProfileRow profile)
        { CompanyProfileStatus.Text = "Select a profile first."; return; }
        if (MessageBox.Show(this, $"Delete '{profile.Name}' from saved company/person profiles?",
            "Confirm profile deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        if (_companyProfileStore is null) return;
        try
        {
            _companyProfileStore.Delete(profile.Id); _companyProfiles.Remove(profile);
            if (_editingCompanyProfileId == profile.Id) { _editingCompanyProfileId = null; SaveCompanyProfileButton.Content = "Save Profile"; }
            CompanyProfileCount.Text = _companyProfiles.Count.ToString();
            RefreshProductProfileChoices();
            CompanyProfileStatus.Text = $"Deleted '{profile.Name}'.";
        }
        catch (Exception ex) { CompanyProfileStatus.Text = $"Profile could not be deleted: {ex.Message}"; }
    }

    private void ClearCompanyProfile_Click(object sender, RoutedEventArgs e)
    {
        _editingCompanyProfileId = null; CompanyProfileType.SelectedIndex = 0;
        CompanyProfileName.Clear(); CompanyProfileAddress.Clear(); CompanyProfileContact.Clear();
        SaveCompanyProfileButton.Content = "Save Profile"; CompanyProfileStatus.Text = "Ready for a new profile.";
    }


    private void OpenWebsite_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://www.rssapps.net") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not open the RSS website. {ex.Message}",
                "Open website", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SendContactMessage_Click(object sender, RoutedEventArgs e)
    {
        var name = ContactName.Text.Trim();
        var email = ContactEmail.Text.Trim();
        var phone = ContactPhone.Text.Trim();
        var subject = ContactSubject.Text.Trim();
        var message = ContactMessage.Text.Trim();

        if (name.Length == 0 || email.Length == 0 || subject.Length == 0 || message.Length == 0)
        {
            ContactStatus.Text = "Please complete your name, email, subject and message.";
            return;
        }
        if (name.Length > 120 || email.Length > 254 || phone.Length > 40 ||
            subject.Length > 180 || message.Length > 5000)
        {
            ContactStatus.Text = "One or more fields exceed the allowed length. Please shorten your message.";
            return;
        }
        try
        {
            _ = new MailAddress(email);
        }
        catch (FormatException)
        {
            ContactStatus.Text = "Enter a valid reply email address.";
            return;
        }

        var body = $"Name: {name}\nEmail: {email}\nPhone: {phone}\n\nMessage:\n{message}";
        var mailto = "mailto:riyaz@rssapps.net?subject=" + Uri.EscapeDataString(subject) +
                     "&body=" + Uri.EscapeDataString(body);
        try
        {
            Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });
            ContactStatus.Text = "Your default email app has opened with this message. Review it and press Send to deliver it to RSS.";
        }
        catch (Exception ex)
        {
            ContactStatus.Text = $"Could not open an email app. You can email riyaz@rssapps.net directly. Details: {ex.Message}";
        }
    }

    private void ClearContactForm_Click(object sender, RoutedEventArgs e)
    {
        ContactName.Clear();
        ContactEmail.Clear();
        ContactPhone.Clear();
        ContactSubject.Clear();
        ContactMessage.Clear();
        ContactStatus.Text = "Form cleared.";
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
        PreviewImporter.Text = string.IsNullOrWhiteSpace(ImportByName.Text) && string.IsNullOrWhiteSpace(ImportByAddress.Text)
            ? "" : $"Import By: {ImportByName.Text.Trim()} {ImportByAddress.Text.Trim()}".Trim();
        PreviewDistributor.Text = string.IsNullOrWhiteSpace(DistributedByName.Text) && string.IsNullOrWhiteSpace(DistributedByAddress.Text)
            ? "" : $"Distributed By: {DistributedByName.Text.Trim()} {DistributedByAddress.Text.Trim()}".Trim();
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

    private void PrintSelectedLabel_Click(object sender, RoutedEventArgs e) => OpenPrintPreview_Click(sender, e);

    private void SelectAllPrintProducts_Click(object sender, RoutedEventArgs e)
    {
        foreach (var product in _products) product.IsPrintSelected = true;
        PrintProductsList.Items.Refresh();
    }

    private void ClearPrintProducts_Click(object sender, RoutedEventArgs e)
    {
        foreach (var product in _products) product.IsPrintSelected = false;
        PrintProductsList.Items.Refresh();
    }

    private void OpenPrintPreview_Click(object sender, RoutedEventArgs e)
    {
        var selectedProducts = _products.Where(p => p.IsPrintSelected).ToList();
        if (selectedProducts.Count == 0)
        {
            PrintStatus.Text = "Tick one or more products in Items on This Sheet.";
            return;
        }
        if (!TryReadMillimetres(LabelWidth.Text, out var labelWidthMm) ||
            !TryReadMillimetres(LabelHeight.Text, out var labelHeightMm) ||
            labelWidthMm <= 0 || labelHeightMm <= 0 || labelWidthMm > 500 || labelHeightMm > 500)
        {
            PrintStatus.Text = "Set valid label dimensions in the Designer first.";
            return;
        }
        if (!TryGetPrintPageSettings(out var pageWidthMm, out var pageHeightMm, out var marginLeftMm, out var marginTopMm, out var marginRightMm, out var marginBottomMm, out var horizontalGapMm, out var verticalGapMm, out var orientation))
            return;

        // Expand each selected product into its requested number of copies, preserving selection order.
        var printQueue = new List<ProductRow>();
        foreach (var product in selectedProducts)
        {
            var copies = int.TryParse(product.Copies, NumberStyles.None, CultureInfo.CurrentCulture, out var parsedCopies)
                ? Math.Clamp(parsedCopies, 1, 500) : 1;
            for (var copy = 0; copy < copies; copy++) printQueue.Add(product);
        }
        if (printQueue.Count > 5000)
        {
            PrintStatus.Text = "The selected products exceed 5,000 labels in one print job. Reduce the copy counts.";
            return;
        }

        // Compare normal and 90-degree layouts, then choose the orientation that fits most labels.
        var printableWidthMm = pageWidthMm - marginLeftMm - marginRightMm;
        var printableHeightMm = pageHeightMm - marginTopMm - marginBottomMm;
        var normalColumns = (int)Math.Floor((printableWidthMm + horizontalGapMm) / (labelWidthMm + horizontalGapMm));
        var normalRows = (int)Math.Floor((printableHeightMm + verticalGapMm) / (labelHeightMm + verticalGapMm));
        var rotatedColumns = (int)Math.Floor((printableWidthMm + horizontalGapMm) / (labelHeightMm + horizontalGapMm));
        var rotatedRows = (int)Math.Floor((printableHeightMm + verticalGapMm) / (labelWidthMm + verticalGapMm));
        var normalCount = Math.Max(0, normalColumns) * Math.Max(0, normalRows);
        var rotatedCount = Math.Max(0, rotatedColumns) * Math.Max(0, rotatedRows);
        var rotated = rotatedCount > normalCount;
        var columns = rotated ? rotatedColumns : normalColumns;
        var rows = rotated ? rotatedRows : normalRows;
        var footprintWidthMm = rotated ? labelHeightMm : labelWidthMm;
        var footprintHeightMm = rotated ? labelWidthMm : labelHeightMm;
        var perPage = rotated ? rotatedCount : normalCount;
        if (perPage < 1)
        {
            PrintStatus.Text = "The selected label does not fit inside the page margins, even after 90° rotation.";
            return;
        }
        var usedWidthMm = columns * footprintWidthMm + Math.Max(0, columns - 1) * horizontalGapMm;
        var usedHeightMm = rows * footprintHeightMm + Math.Max(0, rows - 1) * verticalGapMm;
        var startLeftMm = marginLeftMm + (printableWidthMm - usedWidthMm) / 2m;
        var startTopMm = marginTopMm + (printableHeightMm - usedHeightMm) / 2m;
        var pageCount = (int)Math.Ceiling(printQueue.Count / (double)perPage);
        var pages = new List<DrawingVisual>();
        for (var page = 0; page < pageCount; page++)
        {
            var pageProducts = printQueue.Skip(page * perPage).Take(perPage).ToList();
            pages.Add(BuildSheetVisual(pageProducts, pageWidthMm, pageHeightMm, labelWidthMm, labelHeightMm,
                startLeftMm, startTopMm, horizontalGapMm, verticalGapMm, pageProducts.Count, columns, rotated));
        }

        PrintLayoutSummary.Text = $"{pageWidthMm:0.##} × {pageHeightMm:0.##} mm · {orientation}\n{columns} columns × {rows} rows · {(rotated ? "90° rotated labels" : "normal labels")} · auto-centred\n{perPage} labels per page · {pageCount} page(s)\n{selectedProducts.Count} different products · {printQueue.Count} total labels";
        var preview = new PrintPreviewWindow(pages, pageWidthMm, pageHeightMm, printQueue.Count, perPage,
            () => PrintSheetVisuals(pages, pageWidthMm, pageHeightMm, orientation, PrinterList.SelectedItem?.ToString()));
        preview.Owner = this;
        preview.ShowDialog();
        PrintStatus.Text = "Mixed-product preview is ready. Check each label before printing.";
    }

    private bool TryGetPrintPageSettings(out decimal pageWidthMm, out decimal pageHeightMm, out decimal marginLeftMm, out decimal marginTopMm, out decimal marginRightMm, out decimal marginBottomMm, out decimal horizontalGapMm, out decimal verticalGapMm, out PageOrientation orientation)
    {
        pageWidthMm = 210m; pageHeightMm = 297m; marginLeftMm = 8m; marginTopMm = 8m; marginRightMm = 8m; marginBottomMm = 8m; horizontalGapMm = 2m; verticalGapMm = 2m; orientation = PageOrientation.Portrait;
        var selected = PrintPaperSize.SelectedItem is ComboBoxItem item ? item.Content?.ToString() : "A4";
        switch (selected)
        {
            case "A5": pageWidthMm = 148m; pageHeightMm = 210m; break;
            case "Letter": pageWidthMm = 215.9m; pageHeightMm = 279.4m; break;
            case "Custom":
                if (!TryReadMillimetres(CustomPageWidth.Text, out pageWidthMm) ||
                    !TryReadMillimetres(CustomPageHeight.Text, out pageHeightMm) ||
                    pageWidthMm < 25m || pageHeightMm < 25m || pageWidthMm > 1000m || pageHeightMm > 1000m)
                {
                    PrintStatus.Text = "Custom page width and height must be between 25 and 1000 mm.";
                    return false;
                }
                break;
        }
        if (PrintOrientation.SelectedIndex == 1)
        {
            (pageWidthMm, pageHeightMm) = (pageHeightMm, pageWidthMm);
            orientation = PageOrientation.Landscape;
        }
        if (!TryReadMillimetres(MarginLeft.Text, out marginLeftMm) || marginLeftMm < 0m ||
            !TryReadMillimetres(MarginTop.Text, out marginTopMm) || marginTopMm < 0m ||
            !TryReadMillimetres(MarginRight.Text, out marginRightMm) || marginRightMm < 0m ||
            !TryReadMillimetres(MarginBottom.Text, out marginBottomMm) || marginBottomMm < 0m ||
            !TryReadMillimetres(HorizontalLabelGap.Text, out horizontalGapMm) || horizontalGapMm < 0m ||
            !TryReadMillimetres(VerticalLabelGap.Text, out verticalGapMm) || verticalGapMm < 0m ||
            marginLeftMm + marginRightMm >= pageWidthMm || marginTopMm + marginBottomMm >= pageHeightMm)
        {
            PrintStatus.Text = "Enter valid non-negative margins and label gaps. Opposing margins must leave usable page space.";
            return false;
        }
        return true;
    }

    private DrawingVisual BuildSheetVisual(IReadOnlyList<ProductRow> products, decimal pageWidthMm, decimal pageHeightMm,
        decimal labelWidthMm, decimal labelHeightMm, decimal marginLeftMm, decimal marginTopMm,
        decimal horizontalGapMm, decimal verticalGapMm, int labelCount, int columns, bool rotateLabels)
    {
        const double mmToDip = 96d / 25.4d;
        var pageWidth = (double)pageWidthMm * mmToDip;
        var pageHeight = (double)pageHeightMm * mmToDip;
        var labelWidth = (double)labelWidthMm * mmToDip;
        var labelHeight = (double)labelHeightMm * mmToDip;
        var marginLeft = (double)marginLeftMm * mmToDip;
        var marginTop = (double)marginTopMm * mmToDip;
        var horizontalGap = (double)horizontalGapMm * mmToDip;
        var verticalGap = (double)verticalGapMm * mmToDip;
        var visual = new DrawingVisual();
        using var dc = visual.RenderOpen();
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, pageWidth, pageHeight));
        for (var i = 0; i < labelCount; i++)
        {
            var col = i % columns;
            var row = i / columns;
            var footprintWidth = rotateLabels ? labelHeight : labelWidth;
            var footprintHeight = rotateLabels ? labelWidth : labelHeight;
            var x = marginLeft + col * (footprintWidth + horizontalGap);
            var y = marginTop + row * (footprintHeight + verticalGap);
            if (rotateLabels)
            {
                // Rotate around the top-right corner so the label occupies its swapped footprint.
                dc.PushTransform(new TranslateTransform(x + footprintWidth, y));
                dc.PushTransform(new RotateTransform(90));
            }
            else
            {
                dc.PushTransform(new TranslateTransform(x, y));
            }
            // Clip all artwork, including the watermark, to this individual label.
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, labelWidth, labelHeight)));
            DrawProductLabel(dc, products[i], labelWidth, labelHeight);
            dc.Pop(); // label clip
            if (rotateLabels) dc.Pop(); // rotation
            dc.Pop(); // label position
        }
        return visual;
    }

    private void DrawProductLabel(DrawingContext dc, ProductRow product, double width, double height)
    {
        dc.DrawRectangle(Brushes.White, new Pen(Brushes.Black, 0.7), new Rect(0, 0, width, height));
        var pad = Math.Max(5d, Math.Min(width, height) * 0.045d);
        var watermark = _watermarkStore?.Load() ?? (Image: (byte[]?)null, Opacity: 0d);
        BitmapSource? watermarkImage = watermark.Image is { Length: > 0 } bytes ? DecodeWatermark(bytes) : null;
        if (watermarkImage is not null && watermark.Opacity > 0)
        {
            dc.PushOpacity(Math.Clamp(watermark.Opacity, 0d, 1d));
            dc.DrawImage(watermarkImage, new Rect(pad, pad, Math.Max(1, width - 2 * pad), Math.Max(1, height - 2 * pad)));
            dc.Pop();
        }
        var compact = Math.Min(width, height) / 100d;
        var titleSize = Math.Clamp(compact * 8.5d, 7d, 24d);
        var bodySize = Math.Clamp(compact * 3.7d, 5d, 11d);
        var smallSize = Math.Clamp(compact * 2.8d, 4.5d, 8d);
        var y = pad;
        DrawLabelText(dc, product.English.ToUpperInvariant(), pad, y, width - 2 * pad, titleSize, true, Brushes.DarkRed, TextAlignment.Center);
        y += titleSize * 1.35d;
        var localName = string.Join("  •  ", new[] { product.Sinhala, product.Tamil }.Where(v => !string.IsNullOrWhiteSpace(v)));
        if (localName.Length > 0)
        {
            DrawLabelText(dc, localName, pad, y, width - 2 * pad, bodySize, false, Brushes.DarkRed, TextAlignment.Center);
            y += bodySize * 1.5d;
        }
        var metadata = new[]
        {
            FormatPrintRow("M.F.D", product.Mfd, product.MfdPrintOnPack),
            FormatPrintRow("EXP.D", product.Expiry, product.ExpiryPrintOnPack),
            FormatPrintRow("B.No", product.Batch, product.BatchPrintOnPack),
            FormatPrintRow("N.Wt", product.NetWeight, product.NetWeightPrintOnPack)
        };
        foreach (var line in metadata.Where(v => v.Length > 0))
        {
            DrawLabelText(dc, line, pad, y, width - 2 * pad, bodySize, false, Brushes.Black, TextAlignment.Left);
            y += bodySize * 1.25d;
        }
        foreach (var party in new[]
        {
            ("Import By", product.ImportByName, product.ImportByAddress),
            ("Distributed By", product.DistributedByName, product.DistributedByAddress)
        })
        {
            if (string.IsNullOrWhiteSpace(party.Item2) && string.IsNullOrWhiteSpace(party.Item3)) continue;
            DrawLabelText(dc, $"{party.Item1}: {party.Item2} {party.Item3}".Trim(), pad, y, width - 2 * pad, smallSize, false, Brushes.Black, TextAlignment.Left);
            y += smallSize * 1.25d;
        }
        var mrpSize = Math.Clamp(compact * 7d, 6d, 18d);
        DrawLabelText(dc, $"M.R.P. (Rs.) {product.Mrp}", pad, Math.Min(y, height * 0.62d), width - 2 * pad, mrpSize, true, Brushes.Black, TextAlignment.Center);
        if (product.ShowPerPiecePrice)
        {
            var perPiece = product.PerPiecePricingMode == "Manual"
                ? product.ManualPerPiecePrice
                : decimal.TryParse(product.BoxPrice, NumberStyles.Number, CultureInfo.CurrentCulture, out var boxPrice) &&
                  int.TryParse(product.PiecesPerBox, NumberStyles.None, CultureInfo.CurrentCulture, out var pieces) && pieces > 0
                    ? (boxPrice / pieces).ToString("0.00", CultureInfo.CurrentCulture) : "";
            if (perPiece.Length > 0)
                DrawLabelText(dc, $"Per Piece (Rs.) {perPiece}", pad, Math.Min(y + mrpSize * 1.35d, height * 0.73d), width - 2 * pad, bodySize, true, Brushes.Black, TextAlignment.Center);
        }
        var panelTop = height - pad - Math.Clamp(height * 0.24d, 18d, 55d);
        var panelHeight = height - pad - panelTop;
        var panelWidth = (width - 2 * pad) / 3d;
        var nutrition = new[] { ("SUGAR", product.Sugar, Brushes.Firebrick), ("SALT", product.Salt, Brushes.SeaGreen), ("FAT", product.Fat, Brushes.DarkGoldenrod) };
        for (var i = 0; i < nutrition.Length; i++)
        {
            var rect = new Rect(pad + i * panelWidth, panelTop, panelWidth - 1, panelHeight);
            dc.DrawRectangle(nutrition[i].Item3, null, rect);
            DrawLabelText(dc, nutrition[i].Item1, rect.X + 1, rect.Y + 2, rect.Width - 2, smallSize, true, Brushes.White, TextAlignment.Center);
            DrawLabelText(dc, $"{(string.IsNullOrWhiteSpace(nutrition[i].Item2) ? "—" : nutrition[i].Item2)} g/100g",
                rect.X + 1, rect.Y + smallSize + 4, rect.Width - 2, smallSize, false, Brushes.White, TextAlignment.Center);
        }
    }

    private static void PrintSheetVisuals(IReadOnlyList<DrawingVisual> pages, decimal pageWidthMm, decimal pageHeightMm, PageOrientation orientation, string? selectedPrinter)
    {
        using var server = new LocalPrintServer();
        var dialog = new PrintDialog();
        if (!string.IsNullOrWhiteSpace(selectedPrinter))
            dialog.PrintQueue = server.GetPrintQueue(selectedPrinter);
        if (dialog.ShowDialog() != true) return;
        const double mmToDip = 96d / 25.4d;
        dialog.PrintTicket.PageMediaSize = new PageMediaSize((double)pageWidthMm * mmToDip, (double)pageHeightMm * mmToDip);
        dialog.PrintTicket.PageOrientation = orientation;
        for (var i = 0; i < pages.Count; i++)
            dialog.PrintVisual(pages[i], $"RSS Label Maker - sheet {i + 1} of {pages.Count}");
    }

    private static void DrawLabelText(DrawingContext dc, string text, double x, double y, double maxWidth,
        double fontSize, bool bold, Brush brush, TextAlignment alignment)
    {
        if (string.IsNullOrWhiteSpace(text) || maxWidth <= 0) return;
        var typeface = new Typeface(new FontFamily("Arial"), FontStyles.Normal,
            bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            typeface, fontSize, brush, 1d)
        {
            MaxTextWidth = maxWidth,
            MaxTextHeight = Math.Max(fontSize * 2.4d, fontSize + 1d),
            TextAlignment = alignment,
            Trimming = TextTrimming.CharacterEllipsis
        };
        dc.DrawText(formatted, new Point(x, y));
    }

    private static string FormatPrintRow(string label, string value, bool printOnPack)
    {
        if (string.IsNullOrWhiteSpace(value) && !printOnPack) return "";
        return $"{label}: {(printOnPack ? "Printed on Pack" : value.Trim())}";
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


    private sealed class CompanyProfileRow
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string ProfileType { get; init; } = "Import By";
        public string Name { get; init; } = "";
        public string Address { get; init; } = "";
        public string Contact { get; init; } = "";
    }

    private sealed class CompanyProfileStore
    {
        private readonly string _connectionString;
        public CompanyProfileStore()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Razeen Secure Solution", "RSS Label Maker");
            Directory.CreateDirectory(folder);
            _connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(folder, "rss-label-maker.db"),
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate
            }.ToString();
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE IF NOT EXISTS CompanyProfiles (Id TEXT NOT NULL PRIMARY KEY, ProfileType TEXT NOT NULL, Name TEXT NOT NULL, Address TEXT NOT NULL, Contact TEXT NOT NULL DEFAULT '', CreatedUtc TEXT NOT NULL);";
            command.ExecuteNonQuery();
        }
        public List<CompanyProfileRow> Load()
        {
            var rows = new List<CompanyProfileRow>();
            using var connection = OpenConnection(); using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, ProfileType, Name, Address, Contact FROM CompanyProfiles ORDER BY Name COLLATE NOCASE;";
            using var reader = command.ExecuteReader();
            while (reader.Read()) rows.Add(new CompanyProfileRow { Id=reader.GetString(0), ProfileType=reader.GetString(1), Name=reader.GetString(2), Address=reader.GetString(3), Contact=reader.GetString(4) });
            return rows;
        }
        public void Save(CompanyProfileRow p)
        {
            using var connection=OpenConnection(); using var command=connection.CreateCommand();
            command.CommandText="INSERT INTO CompanyProfiles (Id,ProfileType,Name,Address,Contact,CreatedUtc) VALUES ($id,$type,$name,$address,$contact,$created);";
            AddParameters(command,p); command.Parameters.AddWithValue("$created",DateTime.UtcNow.ToString("O")); command.ExecuteNonQuery();
        }
        public void Update(CompanyProfileRow p)
        {
            using var connection=OpenConnection(); using var command=connection.CreateCommand();
            command.CommandText="UPDATE CompanyProfiles SET ProfileType=$type,Name=$name,Address=$address,Contact=$contact WHERE Id=$id;";
            AddParameters(command,p); if(command.ExecuteNonQuery()!=1) throw new InvalidOperationException("The company/person profile no longer exists.");
        }
        public void Delete(string id)
        {
            using var connection=OpenConnection(); using var command=connection.CreateCommand();
            command.CommandText="DELETE FROM CompanyProfiles WHERE Id=$id;"; command.Parameters.AddWithValue("$id",id);
            if(command.ExecuteNonQuery()!=1) throw new InvalidOperationException("The company/person profile no longer exists.");
        }
        private static void AddParameters(Microsoft.Data.Sqlite.SqliteCommand c, CompanyProfileRow p)
        {
            c.Parameters.AddWithValue("$id",p.Id); c.Parameters.AddWithValue("$type",p.ProfileType);
            c.Parameters.AddWithValue("$name",p.Name); c.Parameters.AddWithValue("$address",p.Address);
            c.Parameters.AddWithValue("$contact",p.Contact);
        }
        private Microsoft.Data.Sqlite.SqliteConnection OpenConnection()
        { var c=new Microsoft.Data.Sqlite.SqliteConnection(_connectionString); c.Open(); return c; }
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
        public string ImportByProfileId { get; init; } = "";
        public string ImportByName { get; init; } = "";
        public string ImportByAddress { get; init; } = "";
        public string DistributedByProfileId { get; init; } = "";
        public string DistributedByName { get; init; } = "";
        public string DistributedByAddress { get; init; } = "";
        public string PiecesPerBox { get; init; } = "";
        public string BoxPrice { get; init; } = "";
        public bool ShowPerPiecePrice { get; init; }
        public string PerPiecePricingMode { get; init; } = "Automatic";
        public string ManualPerPiecePrice { get; init; } = "";
        public string Copies { get; init; } = "1";
        public bool IsPrintSelected { get; set; }
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
