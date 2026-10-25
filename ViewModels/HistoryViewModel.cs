using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BingWallpaperWPF.Models;
using BingWallpaperWPF.Services;

namespace BingWallpaperWPF.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private const int PageSize = 10;

    private readonly IDialogService _dialogService;
    private List<LocalWallpaper> _allWallpapers = new();

    [ObservableProperty]
    private ObservableCollection<LocalWallpaper> _wallpapers = new();

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int _currentPage = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int _pageCount = 1;

    [ObservableProperty]
    private string _pageInfo = string.Empty;

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
            _allWallpapers.Remove(item);
            ShowCurrentPage();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"删除失败: {ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void PreviousPage()
    {
        CurrentPage--;
        ShowCurrentPage();
    }

    private bool CanGoPrevious => CurrentPage > 1;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextPage()
    {
        CurrentPage++;
        ShowCurrentPage();
    }

    private bool CanGoNext => CurrentPage < PageCount;

    public void LoadWallpapers()
    {
        _allWallpapers = DataService.GetLocalWallpapers();
        CurrentPage = 1;
        ShowCurrentPage();
        Logger.Info($"HistoryViewModel 加载历史记录: 数量={_allWallpapers.Count}, IsEmpty={IsEmpty}");
    }

    private void ShowCurrentPage()
    {
        PageCount = Math.Max(1, (_allWallpapers.Count + PageSize - 1) / PageSize);
        CurrentPage = Math.Clamp(CurrentPage, 1, PageCount);
        Wallpapers = new ObservableCollection<LocalWallpaper>(
            _allWallpapers.Skip((CurrentPage - 1) * PageSize).Take(PageSize));
        IsEmpty = _allWallpapers.Count == 0;
        PageInfo = $"第 {CurrentPage} / {PageCount} 页  ·  共 {_allWallpapers.Count} 张";
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
