using System.Windows;
using BingWallpaperWPF.ViewModels;

namespace BingWallpaperWPF.Views;

public partial class HistoryWindow : Window
{
    public HistoryWindow(HistoryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
