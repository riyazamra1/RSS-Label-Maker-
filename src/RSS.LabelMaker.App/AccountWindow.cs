using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RSS.LabelMaker.App;

internal sealed class AccountWindow : Window
{
    private readonly StackPanel _root = new();
    private readonly TextBox _name = new() { Height = 34, Padding = new Thickness(9, 6, 9, 6) };
    private readonly TextBox _email = new() { Height = 34, Padding = new Thickness(9, 6, 9, 6) };
    private readonly PasswordBox _password = new() { Height = 34, Padding = new Thickness(9, 6, 9, 6) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brush("#68717D"), Margin = new Thickness(0, 12, 0, 0) };
    private readonly Button _submit = new() { Height = 42, Background = Brush("#C69A43"), Foreground = Brush("#20242B"), FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0), Margin = new Thickness(0, 16, 0, 0) };
    private readonly Button _resend = new() { Content = "Resend verification email", Height = 34, Background = Brushes.White, BorderBrush = Brush("#D9DDE3"), Margin = new Thickness(0, 8, 0, 0) };
    private readonly StackPanel _nameRow = new();
    private bool _registerMode;

    public AccountWindow()
    {
        Title = "RSS Account — RSS Label Maker";
        Width = 520;
        Height = 650;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#F5F6F8");
        ResizeMode = ResizeMode.NoResize;
        Build();
        Loaded += async (_, _) => await LoadExistingSessionAsync();
    }

    private void Build()
    {
        var card = new Border
        {
            Background = Brushes.White, CornerRadius = new CornerRadius(10),
            Padding = new Thickness(30), Margin = new Thickness(28)
        };
        var stack = new StackPanel();
        card.Child = stack;
        stack.Children.Add(new TextBlock { Text = "RSS ACCOUNT", Foreground = Brush("#C69A43"), FontSize = 11, FontWeight = FontWeights.Bold });
        stack.Children.Add(new TextBlock { Text = "Sign in to your RSS account", Foreground = Brush("#20242B"), FontSize = 25, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 8, 0, 5) });
        stack.Children.Add(new TextBlock { Text = "Cloud backup, restore and license management are linked to your RSS Core account. Label creation and printing remain available offline.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#77808C"), FontSize = 12, Margin = new Thickness(0, 0, 0, 20) });
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        var login = new Button { Content = "Sign in", Padding = new Thickness(18, 8, 18, 8), Margin = new Thickness(0, 0, 8, 14), Background = Brush("#20242B"), Foreground = Brushes.White };
        var register = new Button { Content = "Create account", Padding = new Thickness(18, 8, 18, 8), Margin = new Thickness(0, 0, 0, 14), Background = Brush("#F0F1F3"), Foreground = Brush("#20242B") };
        login.Click += (_, _) => SetMode(false);
        register.Click += (_, _) => SetMode(true);
        tabs.Children.Add(login); tabs.Children.Add(register); stack.Children.Add(tabs);

        _nameRow.Children.Add(FieldLabel("Full name"));
        _nameRow.Children.Add(_name);
        stack.Children.Add(_nameRow);
        stack.Children.Add(FieldLabel("Email address"));
        stack.Children.Add(_email);
        stack.Children.Add(FieldLabel("Password"));
        stack.Children.Add(_password);
        stack.Children.Add(new TextBlock { Text = "Use at least 8 characters.", Foreground = Brush("#89919B"), FontSize = 10, Margin = new Thickness(0, 5, 0, 0) });
        _submit.Click += async (_, _) => await SubmitAsync();
        stack.Children.Add(_submit);
        _resend.Click += async (_, _) => await ResendAsync();
        stack.Children.Add(_resend);
        stack.Children.Add(_status);
        stack.Children.Add(new TextBlock { Text = "Email verification links expire after 24 hours. Check your spam folder if the email is not in your inbox.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#89919B"), FontSize = 10, Margin = new Thickness(0, 18, 0, 0) });
        _root.Children.Add(card);
        Content = _root;
        SetMode(false);
    }

    private async Task LoadExistingSessionAsync()
    {
        try
        {
            var session = await RssCoreAccountClient.GetSessionAsync();
            if (session is not null) ShowSignedIn(session);
        }
        catch (Exception ex) { _status.Text = ex.Message; }
    }

    private void SetMode(bool register)
    {
        _registerMode = register;
        _nameRow.Visibility = register ? Visibility.Visible : Visibility.Collapsed;
        _submit.Content = register ? "Create RSS account" : "Sign in";
        _resend.Visibility = register ? Visibility.Visible : Visibility.Visible;
        _status.Text = register
            ? "Create one RSS account to use cloud services and manage your RLM license."
            : "Sign in with your verified RSS account.";
    }

    private async Task SubmitAsync()
    {
        _submit.IsEnabled = false;
        try
        {
            if (_registerMode)
            {
                _status.Text = await RssCoreAccountClient.RegisterAsync(_name.Text.Trim(), _email.Text.Trim(), _password.Password);
                _password.Clear();
            }
            else
            {
                var session = await RssCoreAccountClient.LoginAsync(_email.Text.Trim(), _password.Password);
                ShowSignedIn(session);
            }
        }
        catch (Exception ex) { _status.Text = ex.Message; }
        finally { _submit.IsEnabled = true; }
    }

    private async Task ResendAsync()
    {
        try { _status.Text = await RssCoreAccountClient.ResendVerificationAsync(_email.Text.Trim()); }
        catch (Exception ex) { _status.Text = ex.Message; }
    }

    private void ShowSignedIn(RssAccountSession session)
    {
        _root.Children.Clear();
        var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10), Padding = new Thickness(32), Margin = new Thickness(28) };
        var stack = new StackPanel();
        card.Child = stack;
        stack.Children.Add(new TextBlock { Text = "RSS ACCOUNT", Foreground = Brush("#C69A43"), FontWeight = FontWeights.Bold, FontSize = 11 });
        stack.Children.Add(new TextBlock { Text = "You are signed in", FontSize = 25, FontWeight = FontWeights.Bold, Foreground = Brush("#20242B"), Margin = new Thickness(0, 12, 0, 6) });
        stack.Children.Add(new TextBlock { Text = session.Name, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brush("#20242B") });
        stack.Children.Add(new TextBlock { Text = session.Email, FontSize = 12, Foreground = Brush("#77808C"), Margin = new Thickness(0, 4, 0, 20) });
        stack.Children.Add(new TextBlock { Text = "LICENSE", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brush("#98702B") });
        stack.Children.Add(new TextBlock { Text = $"{session.Plan} · {session.LicenseStatus}", FontSize = 14, Foreground = Brush("#20242B"), Margin = new Thickness(0, 5, 0, 3) });
        stack.Children.Add(new TextBlock { Text = session.ExpiresAt is null ? "No expiry date returned" : $"Expires: {session.ExpiresAt}", FontSize = 11, Foreground = Brush("#77808C") });
        var cloud = new Button { Content = "Cloud backup & license", Height = 42, Margin = new Thickness(0, 20, 0, 0), Background = Brush("#C69A43"), Foreground = Brush("#20242B"), FontWeight = FontWeights.SemiBold };
        cloud.Click += (_, _) =>
        {
            var services = new BackupLicenseWindow { Owner = this };
            if (services.ShowDialog() == true) { DialogResult = true; Close(); }
        };
        stack.Children.Add(cloud);
        var signOut = new Button { Content = "Sign out", Height = 40, Margin = new Thickness(0, 28, 0, 0), Background = Brush("#20242B"), Foreground = Brushes.White };
        signOut.Click += async (_, _) => { await RssCoreAccountClient.SignOutAsync(); DialogResult = false; Close(); };
        stack.Children.Add(signOut);
        _root.Children.Add(card);
        Title = "RSS Account";
    }

    private static TextBlock FieldLabel(string text) => new()
    {
        Text = text, Foreground = Brush("#404854"), FontWeight = FontWeights.SemiBold,
        FontSize = 11, Margin = new Thickness(0, 10, 0, 5)
    };

    private static SolidColorBrush Brush(string value) => new((Color)ColorConverter.ConvertFromString(value));
}
