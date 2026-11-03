using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RSS.LabelMaker.App;

internal sealed class UpdateWelcomeWindow : Window
{
    public UpdateWelcomeWindow(UpdateWelcomeInfo info)
    {
        Title = "RSS Label Maker — Updated successfully";
        Width = 760;
        Height = 650;
        MinWidth = 620;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = Brushes.White;
        Foreground = new SolidColorBrush(Color.FromRgb(35, 39, 46));

        var root = new DockPanel { Margin = new Thickness(28) };
        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        var later = new Button { Content = "View later", Padding = new Thickness(18, 9, 18, 9), Margin = new Thickness(0, 0, 10, 0) };
        later.Click += (_, _) => { DialogResult = false; Close(); };
        var done = new Button
        {
            Content = "Get started",
            Padding = new Thickness(22, 9, 22, 9),
            Background = new SolidColorBrush(Color.FromRgb(35, 39, 46)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            IsDefault = true
        };
        done.Click += (_, _) => { DialogResult = true; Close(); };
        footer.Children.Add(later);
        footer.Children.Add(done);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var content = new StackPanel();
        var scroll = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(scroll);

        content.Children.Add(new TextBlock
        {
            Text = "UPDATE INSTALLED SUCCESSFULLY",
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(151, 108, 38)),
            Margin = new Thickness(0, 0, 0, 8)
        });
        content.Children.Add(new TextBlock
        {
            Text = $"RSS Label Maker {info.NewVersion}",
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 6)
        });
        content.Children.Add(new TextBlock
        {
            Text = $"Updated from {info.PreviousVersion} · {info.InstalledUtc.ToLocalTime():f}",
            FontSize = 12,
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 0, 0, 22)
        });

        AddHeading(content, "WHAT'S NEW");
        var notes = string.IsNullOrWhiteSpace(info.ReleaseNotes)
            ? "This release does not include published release notes."
            : info.ReleaseNotes.Trim();
        content.Children.Add(new TextBlock
        {
            Text = notes,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20,
            Margin = new Thickness(0, 0, 0, 22)
        });

        AddHeading(content, "NEW FEATURES — HOW TO USE THEM");
        AddStep(content, "1. Review the release notes", "Read the changes above to identify the features added or improved in this version.");
        AddStep(content, "2. Open the relevant workspace", "Use the left navigation to open Products, Label Designer, Templates, Print Manager or Settings, depending on the feature.");
        AddStep(content, "3. Configure and try the new feature", "Follow any feature-specific instructions included in the release notes. Existing product records and local settings are retained by the updater.");
        AddStep(content, "4. Continue your work", "Select Get started to return to the dashboard. Your last saved session is restored using the available session-recovery checkpoint.");

        Content = root;
    }

    private static void AddHeading(Panel panel, string text)
    {
        panel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 10)
        });
    }

    private static void AddStep(Panel panel, string title, string description)
    {
        var block = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        block.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold });
        block.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 3, 0, 0)
        });
        panel.Children.Add(block);
    }
}
