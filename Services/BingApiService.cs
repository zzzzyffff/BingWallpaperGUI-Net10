using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using BingWallpaperWPF.Models;

namespace BingWallpaperWPF.Services;

public static class BingApiService
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/111.0.0.0 Safari/537.36";
    private const string BingApi = "https://www.bing.com/HPImageArchive.aspx?format=js&idx={0}&n={1}&mkt={2}";
    private const string BingBase = "https://bing.com";

    private static readonly HttpClient HttpClient = new HttpClient();

    public static readonly IReadOnlyList<string> ValidResolutions = new[]
    {
        "UHD", "1920x1200", "1920x1080", "1366x768",
        "1280x768", "1024x768", "800x600", "800x480",
    };

    public static readonly IReadOnlyList<string> ValidLocales = new[]
    {
        "zh-CN", "en-US", "en-GB", "ja-JP", "de-DE",
        "fr-FR", "ko-KR", "ru-RU", "it-IT", "es-ES",
    };

    static BingApiService()
    {
        HttpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
    }

    public static async Task<List<WallpaperInfo>> FetchWallpapersAsync(
        string locale = "zh-CN", int n = 8, int idx = 0, int retries = 3, CancellationToken ct = default)
    {
        Exception? lastErr = null;
        for (int attempt = 1; attempt <= retries; attempt++)
        {
            try
            {
                string url = string.Format(BingApi, idx, n, locale);
                using var response = await HttpClient.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<BingApiResponse>(json);
                return data?.Images ?? new List<WallpaperInfo>();
            }
            catch (Exception ex)
            {
                lastErr = ex;
                if (attempt < retries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
                }
            }
        }
        throw new InvalidOperationException($"获取壁纸信息失败 (已重试{retries}次): {lastErr?.Message}", lastErr);
    }

    public static string BuildImageUrl(string baseUrl, string resolution)
    {
        if (resolution == "UHD")
        {
            return baseUrl.Replace("_1920x1080", "_UHD", StringComparison.OrdinalIgnoreCase);
        }
        return baseUrl.Replace("_1920x1080", $"_{resolution}", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task DownloadImageAsync(string url, string destPath, int retries = 3, CancellationToken ct = default)
    {
        Exception? lastErr = null;
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        for (int attempt = 1; attempt <= retries; attempt++)
        {
            try
            {
                using var response = await HttpClient.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();
                await using var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs, ct);
                return;
            }
            catch (Exception ex)
            {
                lastErr = ex;
                if (attempt < retries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
                }
            }
        }
        throw new InvalidOperationException($"下载图片失败 (已重试{retries}次): {lastErr?.Message}", lastErr);
    }

    public static string BuildAbsoluteUrl(string relativeUrl)
    {
        if (relativeUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return relativeUrl;
        return new Uri(new Uri(BingBase), relativeUrl).ToString();
    }

    public static string SanitizeFileName(string title)
    {
        string safe = Regex.Replace(title ?? string.Empty, @"[^\w\u4e00-\u9fff]", "_").Trim('_');
        return string.IsNullOrEmpty(safe) ? "BingWallpaper" : safe;
    }

    public static string GetDefaultResolution()
    {
        int width = (int)SystemParameters.PrimaryScreenWidth;
        int height = (int)SystemParameters.PrimaryScreenHeight;

        var standardRes = ValidResolutions.Where(r => r != "UHD").ToList();
        int screenArea = width * height;
        string bestMatch = "UHD";
        int bestDiff = int.MaxValue;

        foreach (var res in standardRes)
        {
            var parts = res.Split('x');
            int w = int.Parse(parts[0]);
            int h = int.Parse(parts[1]);
            int area = w * h;
            int diff = Math.Abs(area - screenArea);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestMatch = res;
            }
        }

        return bestMatch;
    }
}
