# BingWallpaperGUI (.NET 10 WPF 重构版)

基于原 Python/tkinter 版本重构的 Windows 桌面壁纸工具，使用 **.NET 10 + WPF + MVVM** 实现。

## 功能特性

- 获取 Bing 每日壁纸
- 预览并一键设为桌面背景
- 保存到本地
- 查看历史壁纸（缩略图、标题、版权、日期）
- 开机自动更换壁纸（通过注册表 `HKEY_CURRENT_USER\...\Run`）
- 支持多地区、多分辨率选择
- 深色主题界面
- 支持 `--auto-start` 命令行参数，自动下载并设置壁纸后退出

## 项目结构

```
BingWallpaperGUI-Net10/
├── BingWallpaperWPF.csproj
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs
├── Models/
│   ├── WallpaperInfo.cs
│   ├── LocalWallpaper.cs
│   └── BingApiResponse.cs
├── ViewModels/
│   ├── MainViewModel.cs
│   └── HistoryViewModel.cs
├── Services/
│   ├── BingApiService.cs
│   ├── WallpaperService.cs
│   ├── AutoStartService.cs
│   ├── DataService.cs
│   ├── IDialogService.cs
│   └── DialogService.cs
├── Views/
│   └── HistoryWindow.xaml / HistoryWindow.xaml.cs
├── Converters/
│   ├── BoolToVisibilityConverter.cs
│   ├── NullToVisibilityConverter.cs
│   ├── FilePathToThumbnailConverter.cs
│   └── BingDateConverter.cs
├── icon.ico
└── README.md
```

## 开发环境

- **.NET 10 SDK**（已安装 `10.0.301`）
- **Windows 10/11**
- **Visual Studio 2022** 或 **Visual Studio Code**（含 C# Dev Kit）

### 主要依赖

| 包 | 用途 |
|----|------|
| `CommunityToolkit.Mvvm` | MVVM 源生成器（`ObservableProperty`、`RelayCommand` 等） |

## 构建与运行

```bash
cd E:\MyPrograms\BingWallpaperGUI-Net10
dotnet build
dotnet run
```

## 发布为独立可执行文件

```bash
# 框架依赖发布（推荐，体积小）
dotnet publish -c Release -r win-x64 --self-contained false

# 独立发布（无需安装 .NET 运行时）
dotnet publish -c Release -r win-x64 --self-contained true
```

发布输出位于：

```
bin\Release\net10.0-windows\win-x64\publish\
```

## 数据目录

壁纸和元数据保存在程序所在目录下的 `wallpapers` 文件夹中：

```
wallpapers/
├── 20260622_xxx.jpg
└── .metadata/
    └── 20260622_xxx.jpg.json
```

## 与原 Python 版本的差异

| 项目 | 原 Python/tkinter 版本 | 本 WPF 版本 |
|------|------------------------|-------------|
| 运行时 | Python 3.13 + PIL + tkinter | .NET 10 + WPF |
| 架构 | Code-Behind | MVVM（CommunityToolkit.Mvvm） |
| 打包 | PyInstaller（多文件目录） | `dotnet publish` |
| 图片解码 | PIL + 手动 DLL | WPF BitmapImage（原生支持 JPG/PNG） |
| UI 风格 | tkinter 自绘 | WPF 样式/模板 |
| 自动启动 | 注册表 `Run` 项 | 注册表 `Run` 项 |

## 注意事项

- 首次设置开机启动时，程序会将自己的路径写入注册表，并附加 `--auto-start` 参数。
- 自动启动模式下，窗口会隐藏，下载并设置壁纸后自动退出。
- 壁纸存储目录与程序同级，移动程序时请一并复制 `wallpapers` 文件夹。
