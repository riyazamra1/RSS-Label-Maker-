using System.Windows;
using System.Windows.Controls;

namespace RSS.LabelMaker.App;

public partial class AuthWindow : Window
{
    private readonly AuthApiClient _api = new();
    public event Action? Authenticated;

    public AuthWindow() => InitializeComponent();

    private void SetPanel(string name)
    {
        RegisterPanel.Visibility = name=="register" ? Visibility.Visible : Visibility.Collapsed;
        LoginPanel.Visibility = name=="login" ? Visibility.Visible : Visibility.Collapsed;
        VerifyPanel.Visibility = name=="verify" ? Visibility.Visible : Visibility.Collapsed;
        ForgotPanel.Visibility = name=="forgot" ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = "";
    }
    private void ShowRegister_Click(object sender,RoutedEventArgs e) => SetPanel("register");
    private void ShowLogin_Click(object sender,RoutedEventArgs e) => SetPanel("login");
    private void ShowVerify_Click(object sender,RoutedEventArgs e) => SetPanel("verify");
    private void ShowForgot_Click(object sender,RoutedEventArgs e)
    {
        ForgotEmail.Text=LoginEmail.Text;
        SetPanel("forgot");
    }

    private async void Register_Click(object sender,RoutedEventArgs e)
    {
        var name=RegisterName.Text.Trim(); var email=RegisterEmail.Text.Trim();
        var password=RegisterPassword.Password;
        if(name.Length<2 || !email.Contains('@') || password.Length<12 || password!=ConfirmPassword.Password)
        { StatusText.Text="Enter a valid name and email. Passwords must match and contain at least 12 characters."; return; }
        await RunAsync(async()=>{
            var result=await _api.RegisterAsync(name,email,password);
            VerifyEmail.Text=email;
            SetPanel("verify");
            StatusText.Text=result.Message ?? "If the address is eligible, a verification code has been sent.";
        });
    }

    private async void Verify_Click(object sender,RoutedEventArgs e)
    {
        var email=VerifyEmail.Text.Trim(); var code=VerifyCode.Text.Trim();
        if(string.IsNullOrWhiteSpace(email)||code.Length!=6){StatusText.Text="Enter your email address and the six-digit code.";return;}
        await RunAsync(async()=>{
            var result=await _api.VerifyAsync(email,code);
            if(string.IsNullOrWhiteSpace(result.Token)) throw new InvalidOperationException("Verification succeeded without a device session.");
            DeviceCredentialStore.Save(result.Token);
            CompleteAuthentication();
        });
    }

    private async void Resend_Click(object sender,RoutedEventArgs e)
    {
        var email=VerifyEmail.Text.Trim();
        await RunAsync(async()=>{var r=await _api.ResendAsync(email);StatusText.Text=r.Message??"If verification is pending, a new code will be sent.";});
    }

    private async void Login_Click(object sender,RoutedEventArgs e)
    {
        var email=LoginEmail.Text.Trim(); var password=LoginPassword.Password;
        await RunAsync(async()=>{
            var result=await _api.LoginAsync(email,password);
            if(string.IsNullOrWhiteSpace(result.Token)) throw new InvalidOperationException("Sign-in did not return a device session.");
            DeviceCredentialStore.Save(result.Token);
            CompleteAuthentication();
        });
    }

    private async void Forgot_Click(object sender,RoutedEventArgs e)
    {
        await RunAsync(async()=>{var r=await _api.ForgotAsync(ForgotEmail.Text.Trim());StatusText.Text=r.Message??"If the account exists, a reset code will be sent.";});
    }

    private async void Reset_Click(object sender,RoutedEventArgs e)
    {
        var password=ResetPassword.Password;
        if(password.Length<12){StatusText.Text="New password must contain at least 12 characters.";return;}
        await RunAsync(async()=>{
            var r=await _api.ResetAsync(ForgotEmail.Text.Trim(),ResetCode.Text.Trim(),password);
            SetPanel("login"); LoginEmail.Text=ForgotEmail.Text; LoginPassword.Clear();
            StatusText.Text=r.Message??"Password reset. Sign in again.";
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        SetBusy(true);
        try { await action(); }
        catch(Exception ex) { StatusText.Text=ex.Message; }
        finally { SetBusy(false); }
    }
    private void SetBusy(bool busy)
    {
        foreach(var button in FindVisualChildren<Button>(this)) button.IsEnabled=!busy;
        Mouse.OverrideCursor=busy?System.Windows.Input.Cursors.Wait:null;
    }
    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T:DependencyObject
    {
        for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);i++)
        {
            var child=System.Windows.Media.VisualTreeHelper.GetChild(parent,i);
            if(child is T typed) yield return typed;
            foreach(var nested in FindVisualChildren<T>(child)) yield return nested;
        }
    }
    private void CompleteAuthentication()
    {
        Authenticated?.Invoke();
        DialogResult=true;
        Close();
    }
}