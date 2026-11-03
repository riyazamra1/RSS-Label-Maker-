using System.Net.Http;
using System.Windows;

namespace RSS.LabelMaker.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var token = DeviceCredentialStore.Read();
        if (!string.IsNullOrWhiteSpace(token))
        {
            try
            {
                if (!await new AuthApiClient().ValidateAsync(token))
                {
                    DeviceCredentialStore.Clear();
                    token = null;
                }
            }
            catch (HttpRequestException)
            {
                // An already-authorized PC is allowed to continue working offline.
            }
            catch (TaskCanceledException)
            {
                // Network timeout: preserve access to local work on an already-authorized PC.
            }
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            var auth = new AuthWindow();
            auth.ShowDialog();
            token = DeviceCredentialStore.Read();
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            Shutdown();
            return;
        }

        var main = new MainWindow();
        MainWindow = main;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        main.Show();
    }
}