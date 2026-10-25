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
    private const int PreviewDecodePixelWidth = 1600;

    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _runningCts;
    private string? _currentImagePath;
    private string? _currentResolution;
    private string? _currentLocale;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FetchCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetWallpaperCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
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

        Logger.Info($"初始化主窗口：默认分辨率={SelectedResolution}, 地区={SelectedLocale}");
    }

    public event EventHandler? RequestOpenHistory;
    public event EventHandler? RequestClose;

    [RelayCommand(CanExecute = nameof(CanFetch))]
    private async Task FetchAsync()
    {
        await RunFetchAsync(setAfter: false);
    }

    private bool CanFetch => !IsBusy;
    private bool CanSetWallpaper => HasDownloadedImage && !IsBusy;
    private bool CanSave => HasDownloadedImage && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanSetWallpaper))]
    private async Task SetWallpaperAsync()
    {
        if (string.IsNullOrEmpty(_currentImagePath))
            return;

        var selectedRes = SelectedResolution;
        if (_currentResolution != selectedRes || _currentLocale != SelectedLocale)
        {
            await RunFetchAsync(setAfter: true);
            return;
        }

        ApplyWallpaper();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
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
        bool oldValue = IsAutoStartEnabled;
        bool newValue = !oldValue;
        try
        {
            AutoStartService.SetAutoStart(newValue);
            IsAutoStartEnabled = newValue;
            StatusText = $"开机自动启动: {(newValue ? "已开启" : "已关闭")}";
        }
        catch (Exception ex)
        {
            IsAutoStartEnabled = oldValue;
            _dialogService.ShowError($"设置开机启动失败: {ex.Message}");
        }
    }

    public async Task RunAutoStartAsync()
    {
        Logger.Info($"自动启动模式开始：分辨率={SelectedResolution}, 地区={SelectedLocale}");
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        try
        {
            StatusText = "自动模式：获取壁纸中...";
            await DoFetchAsync(setAfter: true, cts.Token);
            StatusText = "壁纸已更新，程序退出";
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            StatusText = "自动模式更新超时，程序退出";
            Logger.Warning(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = $"自动模式更新失败: {ex.Message}";
            Logger.Error("自动模式失败", ex);
        }
        finally
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task RunFetchAsync(bool setAfter)
    {
        var previousCts = _runningCts;
        var cts = new CancellationTokenSource();
        _runningCts = cts;
        previousCts?.Cancel();
        var ct = cts.Token;

        IsBusy = true;
        try
        {
            await DoFetchAsync(setAfter, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Logger.Info("壁纸获取已取消");
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(_runningCts, cts))
                _dialogService.ShowError(ex.Message);
            else
                Logger.Error("已替换的壁纸获取任务失败", ex);
        }
        finally
        {
            if (ReferenceEquals(_runningCts, cts))
            {
                _runningCts = null;
                IsBusy = false;
            }
            cts.Dispose();
        }
    }

    private async Task DoFetchAsync(bool setAfter, CancellationToken ct)
    {
        var locale = SelectedLocale ?? "zh-CN";
        var resolution = SelectedResolution ?? "1920x1080";

        StatusText = "正在从 Bing 获取壁纸...";
        var images = await BingApiService.FetchWallpapersAsync(locale: locale, n: 1, ct: ct);

        if (images.Count == 0)
            throw new InvalidOperationException("Bing 没有返回壁纸数据");

        var wp = images[0];
        string safeTitle = BingApiService.SanitizeFileName(wp.Title);
        string dateStr = !string.IsNullOrEmpty(wp.StartDate)
            ? wp.StartDate
            : DateTime.Now.ToString("yyyyMMdd");
        string fileName = $"{dateStr}_{safeTitle}_{resolution}_{locale}.jpg";
        string destPath = Path.Combine(DataService.DataDirectory, fileName);
        bool reuseExisting = File.Exists(destPath);

        if (reuseExisting)
        {
            StatusText = $"壁纸已存在，直接复用: {fileName}";
            Logger.Info($"复用已下载壁纸: {destPath}");
        }
        else
        {
            string baseUrl = BingApiService.BuildAbsoluteUrl(wp.Url);
            string imgUrl = BingApiService.BuildImageUrl(baseUrl, resolution);
            StatusText = $"下载中: {resolution} ...";
            await BingApiService.DownloadImageAsync(imgUrl, destPath, ct: ct);
        }

        DataService.SaveMetadata(wp, destPath);
        ct.ThrowIfCancellationRequested();

        _currentImagePath = destPath;
        _currentResolution = resolution;
        _currentLocale = locale;

        LoadPreview(destPath);
        UpdateInfo(wp);
        HasDownloadedImage = true;

        StatusText = reuseExisting ? $"已加载已有壁纸: {fileName}" : $"壁纸已下载: {fileName}";

        if (setAfter)
        {
            if (!WallpaperService.SetDesktopWallpaper(destPath))
                throw new InvalidOperationException("无法设置桌面壁纸");
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
            bitmap.DecodePixelWidth = PreviewDecodePixelWidth;
            bitmap.UriSource = new Uri(Path.GetFullPath(path));
            bitmap.EndInit();
            bitmap.Freeze();
            PreviewImage = bitmap;
        }
        catch (Exception ex)
        {
            Logger.Error($"无法加载图片预览: {path}", ex);
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
            _currentLocale = null;
        }
    }

    public void LoadTodayWallpaperIfExists()
    {
        var todayWp = DataService.GetTodayWallpaper();
        if (todayWp == null)
            return;

        _currentImagePath = todayWp.File.FullName;
        _currentResolution = string.IsNullOrEmpty(todayWp.Resolution) ? null : todayWp.Resolution;
        _currentLocale = string.IsNullOrEmpty(todayWp.Locale) ? null : todayWp.Locale;
        LoadPreview(_currentImagePath);

        string dateStr = todayWp.Date;
        if (dateStr.Length == 8)
            dateStr = $"{dateStr[..4]}-{dateStr[4..6]}-{dateStr[6..]}";

        InfoText = string.IsNullOrEmpty(todayWp.Title)
            ? todayWp.File.Name
            : $"{dateStr}  |  {todayWp.Title}  |  {todayWp.Copyright}";

        HasDownloadedImage = true;
        StatusText = $"已加载今日壁纸: {todayWp.File.Name}";
    }

    public void Cleanup()
    {
        CancelRunningTasks();
    }
}
