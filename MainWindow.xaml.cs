using System;
using System.ComponentModel;
using System.Windows;
using BingWallpaperWPF.Services;
using BingWallpaperWPF.ViewModels;
using BingWallpaperWPF.Views;

namespace BingWallpaperWPF;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow()
    {
        InitializeComponent();

        ViewModel = new MainViewModel(new DialogService());
        ViewModel.RequestOpenHistory += OnRequestOpenHistory;
        ViewModel.RequestClose += OnRequestClose;

        DataContext = ViewModel;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        DataService.EnsureDirectories();
        ViewModel.LoadTodayWallpaperIfExists();
    }

    private void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        OpenHistoryWindow();
    }

    private void OnRequestOpenHistory(object? sender, EventArgs e)
    {
        OpenHistoryWindow();
    }

    private void OnRequestClose(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(Close);
    }

    private void OpenHistoryWindow()
    {
        var historyVm = new HistoryViewModel(new DialogService());
        var historyWindow = new HistoryWindow(historyVm)
        {
            Owner = this
        };
        historyWindow.ShowDialog();

        // Refresh main preview metadata if needed
        ViewModel.LoadWallpapersIfNeeded();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        ViewModel.Cleanup();
    }
}
