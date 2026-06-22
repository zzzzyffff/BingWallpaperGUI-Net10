using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
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
    private const int UhdArea = 3840 * 2160;

    private static readonly HttpClient HttpClient = new HttpClient(new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

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
                Logger.Error($"获取壁纸信息失败 (第{attempt}次尝试): {ex.Message}", ex);
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
                Logger.Error($"下载图片失败 (第{attempt}次尝试): {ex.Message}", ex);
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
        var (width, height) = GetPrimaryScreenPhysicalResolution();

        int screenArea = width * height;
        double screenRatio = (double)width / height;

        // 4K 及以上屏幕直接返回 UHD，保证壁纸清晰度
        if (screenArea >= UhdArea)
            return "UHD";

        string bestMatch = "1920x1080";
        int bestScore = int.MaxValue;

        foreach (var res in ValidResolutions.Where(r => r != "UHD"))
        {
            var parts = res.Split('x');
            int w = int.Parse(parts[0]);
            int h = int.Parse(parts[1]);
            int area = w * h;
            double ratio = (double)w / h;

            // 优先匹配宽高比，再匹配面积（避免 2560x1440 误配 1920x1200）
            double ratioDiff = Math.Abs(ratio - screenRatio);
            int areaDiff = Math.Abs(area - screenArea);
            int score = (int)(ratioDiff * 1_000_000) + areaDiff / 1_000;

            if (score < bestScore)
            {
                bestScore = score;
                bestMatch = res;
            }
        }

        return bestMatch;
    }

    private static (int Width, int Height) GetPrimaryScreenPhysicalResolution()
    {
        try
        {
            var devMode = new DEVMODE
            {
                dmDeviceName = new string(new char[32]),
                dmSize = (short)Marshal.SizeOf(typeof(DEVMODE))
            };

            if (EnumDisplaySettings(null!, ENUM_CURRENT_SETTINGS, ref devMode))
            {
                return (devMode.dmPelsWidth, devMode.dmPelsHeight);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("获取物理屏幕分辨率失败，回退到 WPF 设备无关尺寸", ex);
        }

        return ((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);
    }

    private const int ENUM_CURRENT_SETTINGS = -1;

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }
}
