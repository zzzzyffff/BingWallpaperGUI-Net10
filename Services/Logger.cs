using System;
using System.IO;

namespace BingWallpaperWPF.Services;

public static class Logger
{
    private static readonly string LogDirectory;
    private static readonly string LogFilePath;
    private static readonly object Lock = new();

    static Logger()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string fallbackDir = Path.Combine(baseDir, "logs");

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        LogDirectory = !string.IsNullOrEmpty(localAppData)
            ? Path.Combine(localAppData, "BingWallpaperGUI", "logs")
            : fallbackDir;

        try
        {
            Directory.CreateDirectory(LogDirectory);
        }
        catch
        {
            LogDirectory = fallbackDir;
            try { Directory.CreateDirectory(LogDirectory); }
            catch { /* last resort */ }
        }

        LogFilePath = Path.Combine(LogDirectory, $"app_{DateTime.Now:yyyyMMdd}.log");
    }

    public static void Info(string message)
        => Write("INFO", message);

    public static void Error(string message, Exception? exception = null)
    {
        var fullMessage = exception == null
            ? message
            : $"{message}{Environment.NewLine}{exception}";
        Write("ERROR", fullMessage);
    }

    public static void Warning(string message)
        => Write("WARN", message);

    private static void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
        lock (Lock)
        {
            try
            {
                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
            catch
            {
                // Last-resort fallback: nothing more we can do safely here.
            }
        }
    }
}
