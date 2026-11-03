using System.Globalization;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RSS.LabelMaker.App;

internal sealed class PrintPreviewWindow : Window
{
    private readonly IReadOnlyList<DrawingVisual> _pages;
    private readonly decimal _pageWidthMm;
    private readonly decimal _pageHeightMm;
    private readonly int _copies;
    private readonly int _labelsPerPage;
    private readonly Action _printAction;
    private readonly Image _pageImage = new() { Stretch = Stretch.Uniform, SnapsToDevicePixels = true };
    private readonly TextBlock _pageStatus = new() { VerticalAlignment = VerticalAlignment.Center };
    private int _pageIndex;

    public PrintPreviewWindow(IReadOnlyList<DrawingVisual> pages, decimal pageWidthMm, decimal pageHeightMm,
        int copies, int labelsPerPage, Action printAction)
    {
        _pages = pages;
        _pageWidthMm = pageWidthMm;
        _pageHeightMm = pageHeightMm;
        _copies = copies;
        _labelsPerPage = labelsPerPage;
        _printAction = printAction;

        Title = "RSS Label Maker — Print Preview";
        Width = 1050;
        Height = 850;
        MinWidth = 760;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(235, 237, 240));

        var root = new DockPanel { Margin = new Thickness(16) };
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var previous = new Button { Content = "◀ Previous", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0) };
        var next = new Button { Content = "Next ▶", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(8, 0, 16, 0) };
        previous.Click += (_, _) => ShowPage(_pageIndex - 1);
        next.Click += (_, _) => ShowPage(_pageIndex + 1);
        toolbar.Children.Add(previous);
        toolbar.Children.Add(_pageStatus);
        toolbar.Children.Add(next);
        toolbar.Children.Add(new TextBlock
        {
            Text = $"   {_pageWidthMm:0.##} × {_pageHeightMm:0.##} mm  ·  {_copies} labels  ·  {_labelsPerPage} per page",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.DimGray
        });
        var print = new Button
        {
            Content = "Print",
            Padding = new Thickness(22, 8, 22, 8),
            Margin = new Thickness(16, 0, 0, 0),
            Background = new SolidColorBrush(Color.FromRgb(198, 154, 67)),
            Foreground = Brushes.Black
        };
        print.Click += (_, _) =>
        {
            try { _printAction(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Print error", MessageBoxButton.OK, MessageBoxImage.Error); }
        };
        toolbar.Children.Add(print);
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);

        var footer = new TextBlock
        {
            Text = "Preview is shown proportionally. Final physical dimensions depend on the printer's supported media size and printable area.",
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 10, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.FromRgb(220, 223, 227))
        };
        _pageImage.Margin = new Thickness(18);
        scroll.Content = _pageImage;
        root.Children.Add(scroll);
        Content = root;
        ShowPage(0);
    }

    private void ShowPage(int index)
    {
        if (index < 0 || index >= _pages.Count) return;
        _pageIndex = index;
        const double mmToDip = 96d / 25.4d;
        var width = (int)Math.Ceiling((double)_pageWidthMm * mmToDip);
        var height = (int)Math.Ceiling((double)_pageHeightMm * mmToDip);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(_pages[index]);
        bitmap.Freeze();
        _pageImage.Source = bitmap;
        _pageImage.Width = width;
        _pageImage.Height = height;
        _pageStatus.Text = $"Page {index + 1} of {_pages.Count}";
    }
}
