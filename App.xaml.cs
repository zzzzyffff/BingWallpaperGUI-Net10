using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace BingWallpaperWPF;

public partial class App : System.Windows.Application
{
    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [DllImport("shcore.dll")]
    private static extern int SetProcessDpiAwareness(int awareness);

    public static bool IsAutoStartMode { get; private set; }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // DPI awareness
        try
        {
            SetProcessDpiAwareness(1);
        }
        catch
        {
            try
            {
                SetProcessDPIAware();
            }
            catch
            {
                // ignore
            }
        }

        IsAutoStartMode = e.Args.Contains("--auto-start", StringComparer.OrdinalIgnoreCase);

        var mainWindow = new MainWindow();
        if (IsAutoStartMode)
        {
            _ = mainWindow.ViewModel.RunAutoStartAsync();
        }
        else
        {
            mainWindow.Show();
        }
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        // cleanup if needed
    }
}
