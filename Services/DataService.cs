using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using BingWallpaperWPF.Models;

namespace BingWallpaperWPF.Services;

public static class DataService
{
    public static string DataDirectory
    {
        get
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var dir = new DirectoryInfo(baseDir);

            // 开发模式：如果 exe/dll 位于 bin/Debug|Release/... 下，且上级目录包含 .csproj，
            // 则将壁纸保存到项目根目录，与 Python 原版的开发模式行为保持一致。
            while (dir != null)
            {
                if (dir.Name.Equals("bin", StringComparison.OrdinalIgnoreCase))
                {
                    var projectDir = dir.Parent;
                    if (projectDir != null &&
                        Directory.EnumerateFiles(projectDir.FullName, "*.csproj").Any())
                    {
                        return Path.Combine(projectDir.FullName, "wallpapers");
                    }
                }
                dir = dir.Parent;
            }

            // 发布模式：使用 exe 所在目录
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
        Logger.Info($"开始扫描壁纸目录: {DataDirectory}");
        if (!Directory.Exists(DataDirectory))
        {
            Logger.Info("壁纸目录不存在，返回空列表");
            return list;
        }

        var files = new DirectoryInfo(DataDirectory)
            .EnumerateFiles()
            .Where(f => !f.Name.StartsWith("thumb_", StringComparison.OrdinalIgnoreCase))
            .Where(f => IsImageExtension(f.Extension))
            .OrderByDescending(f => f.LastWriteTime)
            .ToList();

        Logger.Info($"扫描完成，找到 {files.Count} 个图片文件");

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
                catch (Exception ex)
                {
                    Logger.Error($"读取壁纸元数据失败: {metaPath}", ex);
                }
            }

            // 尝试从文件名解析分辨率与地区
            TryParseFileName(file.Name, info);

            info.MetadataPath = metaPath;
            list.Add(info);
        }

        Logger.Info($"加载完成，返回 {list.Count} 条历史记录");
        return list;
    }

    private static void TryParseFileName(string fileName, LocalWallpaper info)
    {
        try
        {
            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var match = Regex.Match(nameWithoutExt,
                @"^(\d{8})_(.*)_((?:\d+x\d+)|UHD)_([a-zA-Z]{2}(?:-[a-zA-Z]{2})?)$");
            if (match.Success)
            {
                info.Date = match.Groups[1].Value;
                info.Resolution = match.Groups[3].Value;
                info.Locale = match.Groups[4].Value;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"解析文件名失败: {fileName}", ex);
        }
    }

    public static LocalWallpaper? GetTodayWallpaper()
    {
        var todayPrefix = DateTime.Now.ToString("yyyyMMdd");
        return GetLocalWallpapers()
            .FirstOrDefault(w => w.File.Name.StartsWith(todayPrefix, StringComparison.OrdinalIgnoreCase));
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
