using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RSS.LabelMaker.App;

internal sealed class AccountWindow : Window
{
    private readonly StackPanel _root = new();
    private readonly StackPanel _authPanel = new();
    private readonly StackPanel _recoveryPanel = new() { Visibility = Visibility.Collapsed };
    private readonly TextBox _name = new() { Height = 36 };
    private readonly TextBox _email = new() { Height = 36 };
    private readonly PasswordBox _password = new() { Height = 36 };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brush("#68717D"), Margin = new Thickness(0, 12, 0, 0) };
    private readonly Button _submit = new() { Height = 42, Background = Brush("#C69A43"), Foreground = Brush("#20242B"), FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0), Margin = new Thickness(0, 16, 0, 0) };
    private readonly Button _resend = new() { Content = "Resend verification email", Height = 36, Background = Brushes.White, BorderBrush = Brush("#D9DDE3"), Margin = new Thickness(0, 8, 0, 0) };
    private readonly StackPanel _nameRow = new();
    private readonly TextBox _resetCode = new() { Height = 36, MaxLength = 6 };
    private readonly PasswordBox _newPassword = new() { Height = 36 };
    private readonly PasswordBox _confirmPassword = new() { Height = 36 };
    private readonly TextBlock _recoveryStatus = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brush("#68717D"), Margin = new Thickness(0, 12, 0, 0) };
    private readonly Button _requestCode = new() { Content = "Send reset code", Height = 40, Background = Brush("#C69A43"), Foreground = Brush("#20242B"), Margin = new Thickness(0, 12, 0, 0) };
    private readonly Button _verifyCode = new() { Content = "Verify code", Height = 40, Background = Brush("#20242B"), Foreground = Brushes.White, Margin = new Thickness(0, 12, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Button _resetPassword = new() { Content = "Set new password", Height = 40, Background = Brush("#C69A43"), Foreground = Brush("#20242B"), Margin = new Thickness(0, 12, 0, 0), Visibility = Visibility.Collapsed };
    private string? _resetToken;
    private bool _registerMode;

    public AccountWindow()
    {
        Title = "RSS Account — RSS Label Maker";
        Width = 520;
        Height = 700;
        MinHeight = 620;
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
        stack.Children.Add(new TextBlock { Text = "Sign in to RSS Label Maker", Foreground = Brush("#20242B"), FontSize = 25, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 8, 0, 5) });
        stack.Children.Add(new TextBlock { Text = "Register and sign in with your RSS Core account. Email verification is required before creating, editing or printing labels.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#77808C"), FontSize = 12, Margin = new Thickness(0, 0, 0, 20) });

        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        var login = new Button { Content = "Sign in", Padding = new Thickness(18, 8, 18, 8), Margin = new Thickness(0, 0, 8, 14), Background = Brush("#20242B"), Foreground = Brushes.White };
        var register = new Button { Content = "Create account", Padding = new Thickness(18, 8, 18, 8), Margin = new Thickness(0, 0, 0, 14), Background = Brush("#F0F1F3"), Foreground = Brush("#20242B") };
        login.Click += (_, _) => SetMode(false);
        register.Click += (_, _) => SetMode(true);
        tabs.Children.Add(login); tabs.Children.Add(register); _authPanel.Children.Add(tabs);

        _nameRow.Children.Add(FieldLabel("Full name"));
        _nameRow.Children.Add(_name);
        _authPanel.Children.Add(_nameRow);
        _authPanel.Children.Add(FieldLabel("Email address"));
        _authPanel.Children.Add(_email);
        _authPanel.Children.Add(FieldLabel("Password"));
        _authPanel.Children.Add(_password);
        _authPanel.Children.Add(new TextBlock { Text = "Use at least 8 characters.", Foreground = Brush("#89919B"), FontSize = 10, Margin = new Thickness(0, 5, 0, 0) });
        _submit.Click += async (_, _) => await SubmitAsync();
        _authPanel.Children.Add(_submit);
        _resend.Click += async (_, _) => await ResendAsync();
        _authPanel.Children.Add(_resend);
        var forgot = new Button { Content = "Forgot password?", Background = Brushes.Transparent, Foreground = Brush("#98702B"), HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(0, 8, 8, 4) };
        forgot.Click += (_, _) => ShowRecovery();
        _authPanel.Children.Add(forgot);
        _authPanel.Children.Add(_status);

        _recoveryPanel.Children.Add(new TextBlock { Text = "PASSWORD RECOVERY", Foreground = Brush("#C69A43"), FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 12, 0, 8) });
        _recoveryPanel.Children.Add(new TextBlock { Text = "Reset your RSS account password", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brush("#20242B") });
        _recoveryPanel.Children.Add(new TextBlock { Text = "A six-digit code will be sent to the email address entered on the sign-in screen. The code expires in 10 minutes.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#77808C"), FontSize = 12, Margin = new Thickness(0, 6, 0, 14) });
        _recoveryPanel.Children.Add(FieldLabel("Verification code"));
        _recoveryPanel.Children.Add(_resetCode);
        _requestCode.Click += async (_, _) => await RequestResetCodeAsync();
        _verifyCode.Click += async (_, _) => await VerifyResetCodeAsync();
        _resetPassword.Click += async (_, _) => await CompletePasswordResetAsync();
        _recoveryPanel.Children.Add(_requestCode);
        _recoveryPanel.Children.Add(_verifyCode);
        _recoveryPanel.Children.Add(FieldLabel("New password"));
        _recoveryPanel.Children.Add(_newPassword);
        _recoveryPanel.Children.Add(FieldLabel("Confirm new password"));
        _recoveryPanel.Children.Add(_confirmPassword);
        _recoveryPanel.Children.Add(_resetPassword);
        _recoveryPanel.Children.Add(_recoveryStatus);
        var back = new Button { Content = "Back to sign in", Background = Brushes.Transparent, Foreground = Brush("#98702B"), HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(0, 10, 8, 4) };
        back.Click += (_, _) => { _recoveryPanel.Visibility = Visibility.Collapsed; _authPanel.Visibility = Visibility.Visible; _resetToken = null; };
        _recoveryPanel.Children.Add(back);

        stack.Children.Add(_authPanel);
        stack.Children.Add(_recoveryPanel);
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
        _resend.Visibility = register ? Visibility.Collapsed : Visibility.Visible;
        _status.Text = register
            ? "Create your RSS Label Maker account. Verify the email address to unlock label operations."
            : "Sign in. If your email is not verified, you can still open the dashboard and resend the verification email.";
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

    private void ShowRecovery()
    {
        _authPanel.Visibility = Visibility.Collapsed;
        _recoveryPanel.Visibility = Visibility.Visible;
        _recoveryStatus.Text = "";
        _verifyCode.Visibility = Visibility.Collapsed;
        _newPassword.Visibility = Visibility.Collapsed;
        _confirmPassword.Visibility = Visibility.Collapsed;
        _resetPassword.Visibility = Visibility.Collapsed;
        SetRecoveryFieldLabels(false);
        _resetToken = null;
    }

    private void SetRecoveryFieldLabels(bool showPasswords)
    {
        foreach (var child in _recoveryPanel.Children.OfType<FrameworkElement>())
        {
            if (child is TextBlock block && (block.Text == "New password" || block.Text == "Confirm new password"))
                block.Visibility = showPasswords ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private async Task RequestResetCodeAsync()
    {
        _requestCode.IsEnabled = false;
        try
        {
            _recoveryStatus.Text = await RssCoreAccountClient.RequestPasswordResetAsync(_email.Text.Trim());
            _verifyCode.Visibility = Visibility.Visible;
        }
        catch (Exception ex) { _recoveryStatus.Text = ex.Message; }
        finally { _requestCode.IsEnabled = true; }
    }

    private async Task VerifyResetCodeAsync()
    {
        _verifyCode.IsEnabled = false;
        try
        {
            _resetToken = await RssCoreAccountClient.VerifyPasswordResetCodeAsync(_email.Text.Trim(), _resetCode.Text.Trim());
            _recoveryStatus.Text = "Email code verified. Enter and confirm your new password.";
            _newPassword.Visibility = Visibility.Visible;
            _confirmPassword.Visibility = Visibility.Visible;
            _resetPassword.Visibility = Visibility.Visible;
            SetRecoveryFieldLabels(true);
        }
        catch (Exception ex) { _recoveryStatus.Text = ex.Message; }
        finally { _verifyCode.IsEnabled = true; }
    }

    private async Task CompletePasswordResetAsync()
    {
        if (string.IsNullOrWhiteSpace(_resetToken)) { _recoveryStatus.Text = "Verify your email code first."; return; }
        if (_newPassword.Password.Length < 8) { _recoveryStatus.Text = "New password must contain at least 8 characters."; return; }
        if (_newPassword.Password != _confirmPassword.Password) { _recoveryStatus.Text = "The passwords do not match."; return; }
        _resetPassword.IsEnabled = false;
        try
        {
            _recoveryStatus.Text = await RssCoreAccountClient.ResetPasswordAsync(_email.Text.Trim(), _resetToken, _newPassword.Password);
            _password.Password = _newPassword.Password;
            _newPassword.Clear(); _confirmPassword.Clear(); _resetCode.Clear();
            _resetToken = null;
            _recoveryPanel.Visibility = Visibility.Collapsed;
            _authPanel.Visibility = Visibility.Visible;
            SetMode(false);
            _status.Text = "Password changed successfully. Sign in with your new password.";
        }
        catch (Exception ex) { _recoveryStatus.Text = ex.Message; }
        finally { _resetPassword.IsEnabled = true; }
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
        if (!session.EmailVerified)
        {
            stack.Children.Add(new Border { Background = Brush("#FFF4D6"), CornerRadius = new CornerRadius(6), Padding = new Thickness(12), Child = new TextBlock { Text = "EMAIL NOT VERIFIED — You can enter the dashboard, but creating, editing and printing labels are locked until you verify your email.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#76520C") } });
            var resend = new Button { Content = "Resend verification email", Height = 38, Margin = new Thickness(0, 12, 0, 0) };
            var verificationStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brush("#68717D"), Margin = new Thickness(0, 8, 0, 0) };
            resend.Click += async (_, _) =>
            {
                resend.IsEnabled = false;
                try { verificationStatus.Text = await RssCoreAccountClient.ResendVerificationAsync(session.Email); }
                catch (Exception ex) { verificationStatus.Text = ex.Message; }
                finally { resend.IsEnabled = true; }
            };
            stack.Children.Add(resend);
            stack.Children.Add(verificationStatus);
        }
        else
        {
            stack.Children.Add(new TextBlock { Text = "Email verified", Foreground = Brush("#287A45"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        }
        stack.Children.Add(new TextBlock { Text = "LICENSE", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brush("#98702B"), Margin = new Thickness(0, 18, 0, 0) });
        stack.Children.Add(new TextBlock { Text = $"{session.Plan} · {session.LicenseStatus}", FontSize = 14, Foreground = Brush("#20242B"), Margin = new Thickness(0, 5, 0, 3) });
        stack.Children.Add(new TextBlock { Text = session.ExpiresAt is null ? "No expiry date returned" : $"Expires: {session.ExpiresAt}", FontSize = 11, Foreground = Brush("#77808C") });
        var proceed = new Button { Content = "Continue to dashboard", Height = 42, Margin = new Thickness(0, 26, 0, 0), Background = Brush("#C69A43"), Foreground = Brush("#20242B") };
        proceed.Click += (_, _) => { DialogResult = true; Close(); };
        stack.Children.Add(proceed);
        var signOut = new Button { Content = "Sign out", Height = 36, Margin = new Thickness(0, 8, 0, 0), Background = Brush("#F0F1F3") };
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
