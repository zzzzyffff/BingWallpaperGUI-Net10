using System.Windows;
using Microsoft.Win32;

namespace BingWallpaperWPF.Services;

public class DialogService : IDialogService
{
    public void ShowError(string message, string title = "错误")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInfo(string message, string title = "成功")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowWarning(string message, string title = "提示")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public bool ShowQuestion(string message, string title = "确认")
        => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public string? ShowSaveFileDialog(string fileName, string filter)
    {
        var dialog = new SaveFileDialog
        {
            DefaultExt = ".jpg",
            Filter = filter,
            FileName = fileName
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
