using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BingWallpaperWPF.Models;
using BingWallpaperWPF.Services;

namespace BingWallpaperWPF.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _runningCts;
    private string? _currentImagePath;
    private string? _currentResolution;
    private List<WallpaperInfo> _wallpapers = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FetchCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetWallpaperCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _hasDownloadedImage;

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private string _infoText = string.Empty;

    [ObservableProperty]
    private string? _selectedResolution;

    [ObservableProperty]
    private string? _selectedLocale;

    [ObservableProperty]
    private bool _isAutoStartEnabled;

    [ObservableProperty]
    private BitmapSource? _previewImage;

    public ObservableCollection<string> Resolutions { get; } = new(BingApiService.ValidResolutions);
    public ObservableCollection<string> Locales { get; } = new(BingApiService.ValidLocales);

    public MainViewModel(IDialogService dialogService)
    {
        _dialogService = dialogService;
        SelectedResolution = BingApiService.GetDefaultResolution();
        SelectedLocale = "zh-CN";
        IsAutoStartEnabled = AutoStartService.IsAutoStartEnabled();
    }

    public event EventHandler? RequestOpenHistory;
    public event EventHandler? RequestClose;

    [RelayCommand(CanExecute = nameof(CanFetch))]
    private async Task FetchAsync()
    {
        await RunFetchAsync(setAfter: false);
    }

    private bool CanFetch => !IsBusy;

    [RelayCommand(CanExecute = nameof(HasDownloadedImage))]
    private async Task SetWallpaperAsync()
    {
        if (string.IsNullOrEmpty(_currentImagePath))
            return;

        var selectedRes = SelectedResolution;
        if (_currentResolution != selectedRes)
        {
            await RunFetchAsync(setAfter: true);
            return;
        }

        ApplyWallpaper();
    }

    [RelayCommand(CanExecute = nameof(HasDownloadedImage))]
    private void Save()
    {
        if (string.IsNullOrEmpty(_currentImagePath) || !File.Exists(_currentImagePath))
            return;

        var dest = _dialogService.ShowSaveFileDialog(
            Path.GetFileName(_currentImagePath),
            "JPEG|*.jpg|PNG|*.png|所有文件|*.*");

        if (dest != null)
        {
            try
            {
                File.Copy(_currentImagePath, dest, true);
                StatusText = $"已保存到: {dest}";
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"保存失败: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private void OpenHistory()
    {
        RequestOpenHistory?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ToggleAutoStart()
    {
        bool newValue = !IsAutoStartEnabled;
        try
        {
            AutoStartService.SetAutoStart(newValue);
            IsAutoStartEnabled = newValue;
            StatusText = $"开机自动启动: {(newValue ? "已开启" : "已关闭")}";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"设置开机启动失败: {ex.Message}");
        }
    }

    public async Task RunAutoStartAsync()
    {
        const int maxAttempts = 5;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                StatusText = $"自动模式：获取壁纸中... (第{attempt}次尝试)";
                await DoFetchAsync(setAfter: true, CancellationToken.None);
                StatusText = "壁纸已更新，程序退出";
                await Task.Delay(2000);
                RequestClose?.Invoke(this, EventArgs.Empty);
                return;
            }
            catch (Exception ex)
            {
                if (attempt < maxAttempts)
                {
                    StatusText = $"自动模式失败 (第{attempt}次): {ex.Message}，{attempt * 3}秒后重试...";
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 3));
                }
                else
                {
                    StatusText = $"自动模式最终失败 (已重试{maxAttempts}次): {ex.Message}";
                    await Task.Delay(5000);
                    RequestClose?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    private async Task RunFetchAsync(bool setAfter)
    {
        CancelRunningTasks();
        _runningCts = new CancellationTokenSource();
        var ct = _runningCts.Token;

        IsBusy = true;
        try
        {
            await DoFetchAsync(setAfter, ct);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DoFetchAsync(bool setAfter, CancellationToken ct)
    {
        var locale = SelectedLocale ?? "zh-CN";
        var resolution = SelectedResolution ?? "1920x1080";

        StatusText = "正在从 Bing 获取壁纸...";
        var images = await BingApiService.FetchWallpapersAsync(locale: locale, n: 8, ct: ct);

        if (images.Count == 0)
            throw new InvalidOperationException("Bing 没有返回壁纸数据");

        _wallpapers = images;
        var wp = images[0];

        string safeTitle = BingApiService.SanitizeFileName(wp.Title);
        string dateStr = !string.IsNullOrEmpty(wp.StartDate)
            ? wp.StartDate
            : DateTime.Now.ToString("yyyyMMdd");
        string fileName = $"{dateStr}_{safeTitle}.jpg";
        string destPath = Path.Combine(DataService.DataDirectory, fileName);

        string baseUrl = BingApiService.BuildAbsoluteUrl(wp.Url);
        string imgUrl = BingApiService.BuildImageUrl(baseUrl, resolution);

        StatusText = $"下载中: {resolution} ...";
        await BingApiService.DownloadImageAsync(imgUrl, destPath, ct: ct);
        DataService.SaveMetadata(wp, destPath);

        _currentImagePath = destPath;
        _currentResolution = resolution;

        LoadPreview(destPath);
        UpdateInfo(wp);
        HasDownloadedImage = true;

        StatusText = $"壁纸已下载: {fileName}";

        if (setAfter)
        {
            WallpaperService.SetDesktopWallpaper(destPath);
            StatusText = "壁纸已更新";
        }
    }

    private void LoadPreview(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(Path.GetFullPath(path));
            bitmap.EndInit();
            bitmap.Freeze();
            PreviewImage = bitmap;
        }
        catch (Exception ex)
        {
            PreviewImage = null;
            StatusText = $"无法加载图片: {ex.Message}";
        }
    }

    private void UpdateInfo(WallpaperInfo wp)
    {
        string dateStr = wp.StartDate ?? string.Empty;
        if (dateStr.Length == 8)
            dateStr = $"{dateStr[..4]}-{dateStr[4..6]}-{dateStr[6..]}";

        InfoText = $"{dateStr}  |  {wp.Title}  |  {wp.Copyright}";
    }

    private void ApplyWallpaper()
    {
        if (string.IsNullOrEmpty(_currentImagePath))
            return;

        if (WallpaperService.SetDesktopWallpaper(_currentImagePath))
        {
            StatusText = "桌面壁纸已更新！";
            _dialogService.ShowInfo("壁纸已设置为桌面背景");
        }
        else
        {
            _dialogService.ShowError("无法设置壁纸");
        }
    }

    private void CancelRunningTasks()
    {
        _runningCts?.Cancel();
        _runningCts?.Dispose();
    }

    public void LoadWallpapersIfNeeded()
    {
        if (!string.IsNullOrEmpty(_currentImagePath) && !File.Exists(_currentImagePath))
        {
            HasDownloadedImage = false;
            PreviewImage = null;
            InfoText = string.Empty;
            StatusText = "当前壁纸已从历史记录中删除";
            _currentImagePath = null;
            _currentResolution = null;
        }
    }

    public void Cleanup()
    {
        CancelRunningTasks();
    }
}
