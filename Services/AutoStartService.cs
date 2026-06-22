using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace BingWallpaperWPF.Services;

public static class AutoStartService
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "BingWallpaperGUI";

    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
            if (key == null) return false;
            var value = key.GetValue(AppName);
            return value != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetAutoStart(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true)
            ?? Registry.CurrentUser.CreateSubKey(RegistryKeyPath);

        if (enable)
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{AppName}.exe");
            exePath = Path.GetFullPath(exePath);
            key.SetValue(AppName, $"\"{exePath}\" --auto-start");
        }
        else
        {
            try
            {
                key.DeleteValue(AppName, false);
            }
            catch
            {
                // ignored
            }
        }
    }
}
