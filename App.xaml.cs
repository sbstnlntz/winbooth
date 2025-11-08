using System;
using System.Threading.Tasks;
using System.Windows;
using winbooth.Views;

namespace winbooth;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var splash = new SplashScreenWindow();
        splash.UpdateStatus("Initialisiere Fotobox...");
        splash.Show();

        var minDisplayTask = splash.RunAutomaticProgressAsync(TimeSpan.FromSeconds(3));
        var mainWindowTask = InitializeMainWindowAsync(splash);

        var mainWindow = await mainWindowTask.ConfigureAwait(true);
        splash.UpdateStatus("Fotobox fast einsatzbereit...");

        await minDisplayTask.ConfigureAwait(true);

        splash.UpdateProgress(95);
        splash.UpdateStatus("Bereit zum Start...");

        splash.CompleteProgress();
        await Task.Delay(250).ConfigureAwait(true);

        Current.MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        mainWindow.Show();
        splash.Close();
    }

    private static async Task<MainWindow> InitializeMainWindowAsync(SplashScreenWindow splash)
    {
        splash.UpdateStatus("Lade Benutzeroberflaeche...");
        await Task.Yield();
        return new MainWindow();
    }
}
