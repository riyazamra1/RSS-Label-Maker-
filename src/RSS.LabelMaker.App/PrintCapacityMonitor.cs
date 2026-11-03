using System;
using System.Globalization;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;

namespace RSS.LabelMaker.App;

public partial class MainWindow
{
    private bool _updatingPrintSelection;

    private void PrintCapacityInputs_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePrintCapacityMonitor();
    }

    private void PrintProductSelection_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingPrintSelection)
            return;

        if (EnableMultipleLabels?.IsChecked != true &&
            sender is CheckBox { IsChecked: true, DataContext: ProductRow current })
        {
            _updatingPrintSelection = true;
            try
            {
                foreach (var product in _products)
                    if (!ReferenceEquals(product, current))
                        product.IsPrintSelected = false;
                PrintProductsList.Items.Refresh();
            }
            finally
            {
                _updatingPrintSelection = false;
            }
        }

        UpdatePrintCapacityMonitor();
    }

    private void UpdatePrintCapacityMonitor()
    {
        if (PrintCapacitySummary is null)
            return;

        if (!TryReadMillimetres(LabelWidth.Text, out var labelWidthMm) ||
            !TryReadMillimetres(LabelHeight.Text, out var labelHeightMm) ||
            labelWidthMm <= 0 || labelHeightMm <= 0 ||
            !TryGetPrintPageSettings(out var pageWidthMm, out var pageHeightMm,
                out var marginLeftMm, out var marginTopMm, out var marginRightMm,
                out var marginBottomMm, out var horizontalGapMm, out var verticalGapMm,
                out var orientation))
        {
            PrintCapacitySummary.Text = "Enter valid label dimensions, page size, margins and gaps to calculate page capacity.";
            return;
        }

        var printableWidthMm = pageWidthMm - marginLeftMm - marginRightMm;
        var printableHeightMm = pageHeightMm - marginTopMm - marginBottomMm;
        var normalColumns = (int)Math.Floor((printableWidthMm + horizontalGapMm) / (labelWidthMm + horizontalGapMm));
        var normalRows = (int)Math.Floor((printableHeightMm + verticalGapMm) / (labelHeightMm + verticalGapMm));
        var rotatedColumns = (int)Math.Floor((printableWidthMm + horizontalGapMm) / (labelHeightMm + horizontalGapMm));
        var rotatedRows = (int)Math.Floor((printableHeightMm + verticalGapMm) / (labelWidthMm + verticalGapMm));
        var normalCapacity = Math.Max(0, normalColumns) * Math.Max(0, normalRows);
        var rotatedCapacity = Math.Max(0, rotatedColumns) * Math.Max(0, rotatedRows);
        var capacity = Math.Max(normalCapacity, rotatedCapacity);

        if (capacity < 1)
        {
            PrintCapacitySummary.Text = "No label fits on this page with the current dimensions and margins.";
            return;
        }

        var selected = _products.Where(p => p.IsPrintSelected).ToList();
        var totalLabels = selected.Sum(p =>
            int.TryParse(p.Copies, NumberStyles.None, CultureInfo.CurrentCulture, out var copies)
                ? Math.Clamp(copies, 1, 500) : 1);
        var usedOnFirstPage = Math.Min(totalLabels, capacity);
        var remainingOnFirstPage = capacity - usedOnFirstPage;
        var pages = (int)Math.Ceiling(totalLabels / (double)capacity);
        var differentProducts = selected.Count;
        var orientationText = rotatedCapacity > normalCapacity ? "90° rotated" : "normal";

        PrintCapacitySummary.Text =
            $"{capacity} labels fit per page ({orientationText}).\n" +
            $"First page: {usedOnFirstPage} used · {remainingOnFirstPage} remaining.\n" +
            $"{totalLabels} labels selected from {differentProducts} product(s) · {pages} page(s) required.";
    }
}
