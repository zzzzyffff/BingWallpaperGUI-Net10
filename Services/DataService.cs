using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BingWallpaperWPF.Models;

namespace BingWallpaperWPF.Services;

public static class DataService
{
    public static string DataDirectory
    {
        get
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDir, "wallpapers");
        }
    }

    private static string MetadataDirectory => Path.Combine(DataDirectory, ".metadata");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(MetadataDirectory);
    }

    public static List<LocalWallpaper> GetLocalWallpapers()
    {
        var list = new List<LocalWallpaper>();
        if (!Directory.Exists(DataDirectory))
            return list;

        var files = new DirectoryInfo(DataDirectory)
            .EnumerateFiles()
            .Where(f => !f.Name.StartsWith("thumb_", StringComparison.OrdinalIgnoreCase))
            .Where(f => IsImageExtension(f.Extension))
            .OrderByDescending(f => f.LastWriteTime)
            .ToList();

        foreach (var file in files)
        {
            var metaPath = Path.Combine(MetadataDirectory, $"{file.Name}.json");
            var info = new LocalWallpaper { File = file };

            if (File.Exists(metaPath))
            {
                try
                {
                    var json = File.ReadAllText(metaPath);
                    var data = JsonSerializer.Deserialize<WallpaperInfo>(json);
                    if (data != null)
                    {
                        info.Title = data.Title ?? string.Empty;
                        info.Copyright = data.Copyright ?? string.Empty;
                        info.Date = data.StartDate ?? string.Empty;
                    }
                }
                catch
                {
                    // ignore metadata errors
                }
            }

            info.MetadataPath = metaPath;
            list.Add(info);
        }

        return list;
    }

    public static void SaveMetadata(WallpaperInfo wallpaper, string imagePath)
    {
        EnsureDirectories();
        string fileName = $"{Path.GetFileName(imagePath)}.json";
        string metaPath = Path.Combine(MetadataDirectory, fileName);
        string json = JsonSerializer.Serialize(wallpaper, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(metaPath, json);
    }

    public static void DeleteWallpaper(LocalWallpaper wallpaper)
    {
        if (File.Exists(wallpaper.File.FullName))
            File.Delete(wallpaper.File.FullName);
        if (!string.IsNullOrEmpty(wallpaper.MetadataPath) && File.Exists(wallpaper.MetadataPath))
            File.Delete(wallpaper.MetadataPath);
    }

    private static bool IsImageExtension(string ext)
    {
        return ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".png", StringComparison.OrdinalIgnoreCase);
    }
}
