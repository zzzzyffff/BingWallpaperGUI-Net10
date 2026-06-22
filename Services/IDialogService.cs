namespace BingWallpaperWPF.Services;

public interface IDialogService
{
    void ShowError(string message, string title = "错误");
    void ShowInfo(string message, string title = "成功");
    void ShowWarning(string message, string title = "提示");
    bool ShowQuestion(string message, string title = "确认");
    string? ShowSaveFileDialog(string fileName, string filter);
}
