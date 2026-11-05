using System.Windows;
using System.Windows.Threading;
using UnloyalFront.UI;
using UnloyalFront.Utility;

namespace UnloyalFront;

public partial class App : Application
{
    /// <summary>True while a launch is running, so closing the settings window does not quit the app.</summary>
    public static bool Launching { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += OnUnhandled;

        try { Paths.EnsureAll(); }
        catch (Exception ex) { Fatal("Could not create the app folder", ex); return; }

        var arg = e.Args.FirstOrDefault();

        if (arg == "--launch")
        {
            StartLaunch(null);
        }
        else if (arg != null && (arg.StartsWith("roblox-player:", StringComparison.OrdinalIgnoreCase) ||
                                 arg.StartsWith("roblox:", StringComparison.OrdinalIgnoreCase)))
        {
            if (Bootstrapper.IsValidLaunchUri(arg)) StartLaunch(arg);
            else
            {
                Log.Write("Rejected suspicious launch link.");
                MessageBox.Show("That Roblox link looked invalid, so it was ignored.", "Unloyal Front",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
            }
        }
        else
        {
            new MainWindow().Show();
        }
    }

    /// <summary>Shows the progress window and runs download -> customize -> launch.</summary>
    public void StartLaunch(string? uri)
    {
        Launching = true;
        var win = new LaunchWindow();
        win.Show();

        Task.Run(async () =>
        {
            try
            {
                var boot = new Bootstrapper((msg, pct) => win.Dispatcher.Invoke(() => win.Update(msg, pct)));
                await boot.RunAsync(uri);
                Dispatcher.Invoke(() => Shutdown());
            }
            catch (Exception ex)
            {
                Log.Write("Launch failed", ex);
                Dispatcher.Invoke(() =>
                {
                    win.Hide();
                    MessageBox.Show($"Something went wrong:\n\n{ex.Message}\n\nDetails are in:\n{Log.FilePath}",
                        "Unloyal Front", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                });
            }
        });
    }

    void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Write("Unhandled", e.Exception);
        MessageBox.Show(e.Exception.Message + "\n\nDetails: " + Log.FilePath, "Unloyal Front",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    void Fatal(string what, Exception ex)
    {
        MessageBox.Show($"{what}:\n{ex.Message}", "Unloyal Front", MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown();
    }
}
