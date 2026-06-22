using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using BingWallpaperWPF.Services;
using BingWallpaperWPF.ViewModels;

namespace BingWallpaperWPF;

public partial class App : System.Windows.Application
{
    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [DllImport("shcore.dll")]
    private static extern int SetProcessDpiAwareness(int awareness);

    private const string MutexName = "BingWallpaperGUI_SingleInstance";
    private static Mutex? _singleInstanceMutex;

    public static bool IsAutoStartMode { get; private set; }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        Logger.Info("应用程序启动");

        // Single instance check
        _singleInstanceMutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            Logger.Info("检测到已有实例运行，本次启动退出");
            Shutdown();
            return;
        }

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

        if (IsAutoStartMode)
        {
            var viewModel = new MainViewModel(new DialogService());
            viewModel.RequestClose += (s, _) => Dispatcher.Invoke(Shutdown);
            _ = viewModel.RunAutoStartAsync();
        }
        else
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        Logger.Info("应用程序退出");
    }
}
