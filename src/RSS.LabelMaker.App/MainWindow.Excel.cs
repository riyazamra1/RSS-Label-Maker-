using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace RSS.LabelMaker.App;

public partial class MainWindow
{
    private void ProductSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var view = ProductsGrid.ItemsSource as System.ComponentModel.ICollectionView;
        if (view is null)
        {
            view = new System.Windows.Data.ListCollectionView(_products);
            ProductsGrid.ItemsSource = view;
        }
        var query = ProductSearch.Text.Trim();
        view.Filter = item =>
        {
            if (item is not ProductRow product) return false;
            if (query.Length == 0) return true;
            return product.English.Contains(query, StringComparison.OrdinalIgnoreCase)
                || product.Sinhala.Contains(query, StringComparison.OrdinalIgnoreCase)
                || product.Tamil.Contains(query, StringComparison.OrdinalIgnoreCase)
                || product.Batch.Contains(query, StringComparison.OrdinalIgnoreCase)
                || product.Mrp.Contains(query, StringComparison.OrdinalIgnoreCase);
        };
        view.Refresh();
    }

    private void ExportProductsExcel_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_products.Count == 0)
        {
            ProductStatus.Text = "There are no products to export.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export RSS Label Maker products",
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            AddExtension = true,
            FileName = $"RSS-Label-Maker-Products-{DateTime.Now:yyyyMMdd}.xlsx"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Products");
            var properties = typeof(ProductRow).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && p.CanWrite)
                .ToArray();

            for (var column = 0; column < properties.Length; column++)
            {
                sheet.Cell(1, column + 1).Value = properties[column].Name;
                sheet.Cell(1, column + 1).Style.Font.Bold = true;
                sheet.Cell(1, column + 1).Style.Fill.BackgroundColor = XLColor.LightGoldenrodYellow;
            }

            for (var row = 0; row < _products.Count; row++)
            {
                for (var column = 0; column < properties.Length; column++)
                {
                    var value = properties[column].GetValue(_products[row]);
                    var cell = sheet.Cell(row + 2, column + 1);
                    if (value is bool boolean) cell.Value = boolean;
                    else cell.Value = value?.ToString() ?? string.Empty;
                }
            }

            sheet.SheetView.FreezeRows(1);
            sheet.RangeUsed()?.SetAutoFilter();
            sheet.Columns().AdjustToContents();
            workbook.SaveAs(dialog.FileName);
            ProductStatus.Text = $"Exported {_products.Count} product(s) to Excel.";
        }
        catch (Exception ex)
        {
            ProductStatus.Text = $"Excel export failed: {ex.Message}";
        }
    }

    private void ImportProductsExcel_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_productStore is null)
        {
            ProductStatus.Text = "Import unavailable: local product database is not ready.";
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Import RSS Label Maker products",
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            using var workbook = new XLWorkbook(dialog.FileName);
            var sheet = workbook.Worksheets.FirstOrDefault();
            if (sheet is null || sheet.LastRowUsed() is null || sheet.LastColumnUsed() is null)
            {
                ProductStatus.Text = "The selected Excel workbook is empty.";
                return;
            }

            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lastColumn = sheet.LastColumnUsed()!.ColumnNumber();
            for (var column = 1; column <= lastColumn; column++)
            {
                var header = sheet.Cell(1, column).GetString().Trim();
                if (header.Length > 0) headers[header] = column;
            }

            if (!headers.ContainsKey(nameof(ProductRow.English)))
            {
                ProductStatus.Text = "Import cancelled: the workbook must contain an 'English' column. Export a template first to see all supported column names.";
                return;
            }

            var properties = typeof(ProductRow).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanWrite)
                .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            var staged = new List<ProductRow>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lastRow = sheet.LastRowUsed()!.RowNumber();
            var skipped = 0;

            for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                if (sheet.Row(rowNumber).CellsUsed().All(cell => cell.IsEmpty())) continue;
                var product = new ProductRow();
                foreach (var header in headers)
                {
                    if (!properties.TryGetValue(header.Key, out var property)) continue;
                    var cell = sheet.Cell(rowNumber, header.Value);
                    object value;
                    if (property.PropertyType == typeof(bool))
                    {
                        var raw = cell.GetString().Trim();
                        if (cell.DataType == XLDataType.Boolean) value = cell.GetBoolean();
                        else if (bool.TryParse(raw, out var parsed)) value = parsed;
                        else if (raw == "1" || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)) value = true;
                        else value = false;
                    }
                    else
                    {
                        value = cell.GetString().Trim();
                    }
                    property.SetValue(product, value);
                }

                if (string.IsNullOrWhiteSpace(product.English))
                {
                    skipped++;
                    continue;
                }

                var id = string.IsNullOrWhiteSpace(product.Id) ? Guid.NewGuid().ToString("N") : product.Id;
                if (!seenIds.Add(id))
                {
                    ProductStatus.Text = $"Import cancelled: duplicate product ID '{id}' appears in the workbook (row {rowNumber}). No records were imported.";
                    return;
                }

                if (product.Id != id)
                {
                    var idProperty = properties[nameof(ProductRow.Id)];
                    idProperty.SetValue(product, id);
                }
                if (string.IsNullOrWhiteSpace(product.Copies))
                    properties[nameof(ProductRow.Copies)].SetValue(product, "1");
                staged.Add(product);
            }

            if (staged.Count == 0)
            {
                ProductStatus.Text = $"No valid product rows found. Skipped {skipped} blank or unnamed row(s).";
                return;
            }

            var added = 0;
            var updated = 0;
            foreach (var product in staged)
            {
                var existing = _products.FirstOrDefault(p => p.Id == product.Id);
                if (existing is null)
                {
                    _productStore.Save(product);
                    _products.Add(product);
                    added++;
                }
                else
                {
                    _productStore.Update(product);
                    var index = _products.IndexOf(existing);
                    _products[index] = product;
                    updated++;
                }
            }

            ProductCount.Text = _products.Count.ToString(CultureInfo.InvariantCulture);
            if (ProductsGrid.ItemsSource is System.ComponentModel.ICollectionView productsView) productsView.Refresh();
            ProductsGrid.Items.Refresh();
            PrintProductsList.Items.Refresh();
            UpdatePreview();
            ProductStatus.Text = $"Excel import complete: {added} added, {updated} updated, {skipped} skipped. Existing products were matched by ID.";
        }
        catch (Exception ex)
        {
            ProductStatus.Text = $"Excel import failed: {ex.Message}";
        }
    }
}
