using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BingWallpaperWPF.Models;
using BingWallpaperWPF.Services;

namespace BingWallpaperWPF.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<LocalWallpaper> _wallpapers = new();

    [ObservableProperty]
    private bool _isEmpty = true;

    public HistoryViewModel(IDialogService dialogService)
    {
        _dialogService = dialogService;
        LoadWallpapers();
    }

    [RelayCommand]
    private void SetWallpaper(LocalWallpaper? item)
    {
        if (item == null)
            return;

        if (WallpaperService.SetDesktopWallpaper(item.File.FullName))
            _dialogService.ShowInfo("壁纸已设置为桌面背景");
        else
            _dialogService.ShowError("无法设置壁纸");
    }

    [RelayCommand]
    private void OpenFile(LocalWallpaper? item)
    {
        if (item == null)
            return;

        try
        {
            Process.Start(new ProcessStartInfo(item.File.FullName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"打开失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Delete(LocalWallpaper? item)
    {
        if (item == null)
            return;

        if (!_dialogService.ShowQuestion($"确定要删除这张壁纸吗？\n\n{item.File.Name}"))
            return;

        try
        {
            DataService.DeleteWallpaper(item);
            LoadWallpapers();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"删除失败: {ex.Message}");
        }
    }

    public void LoadWallpapers()
    {
        var wallpapers = DataService.GetLocalWallpapers();
        Wallpapers = new ObservableCollection<LocalWallpaper>(wallpapers);
        IsEmpty = Wallpapers.Count == 0;
        Logger.Info($"HistoryViewModel 加载历史记录: 数量={Wallpapers.Count}, IsEmpty={IsEmpty}");
    }

    public static BitmapImage? LoadThumbnail(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(Path.GetFullPath(path));
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
