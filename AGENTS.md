# BingWallpaperGUI-Net10 — Agent 开发指南

## 1. 项目简介

**BingWallpaperGUI-Net10** 是原 Python/tkinter 版本 Bing 壁纸工具的重构版，基于 **.NET 10 + WPF** 实现。

- **核心功能**：获取 Bing 每日壁纸、预览、一键设为桌面背景、保存到本地、查看历史记录
- **特点**：无后台驻留、支持开机自动更换壁纸、支持多地区和多分辨率选择
- **目标平台**：Windows 10/11
- **界面风格**：深色主题（`#1e1e1e`），使用 `Microsoft YaHei UI` 字体

## 2. 项目结构

```
BingWallpaperGUI-Net10/
├── BingWallpaperWPF.csproj         # 项目文件
├── app.manifest                    # DPI 感知、长路径等 Windows 清单
├── App.xaml / App.xaml.cs          # 应用入口、DPI 感知、自动启动检测、单实例
├── MainWindow.xaml / MainWindow.xaml.cs   # 主窗口（View）
├── Models/                         # 数据模型
│   ├── WallpaperInfo.cs            # Bing API 壁纸元数据
│   ├── LocalWallpaper.cs           # 本地已下载壁纸信息
│   └── BingApiResponse.cs          # Bing API 响应包装
├── ViewModels/                     # MVVM 视图模型
│   ├── MainViewModel.cs            # 主窗口 ViewModel
│   └── HistoryViewModel.cs         # 历史窗口 ViewModel
├── Services/                       # 业务逻辑服务
│   ├── BingApiService.cs           # 调用 Bing API、下载图片、分辨率适配
│   ├── WallpaperService.cs         # 通过 Win32 API 设置桌面壁纸
│   ├── AutoStartService.cs         # 注册表开机启动管理
│   ├── DataService.cs              # 本地壁纸与元数据管理
│   ├── Logger.cs                   # 文件日志（%LOCALAPPDATA%\BingWallpaperGUI\logs）
│   ├── IDialogService.cs           # 对话框服务接口
│   └── DialogService.cs            # 对话框服务实现（MessageBox、文件对话框）
├── Views/                          # 子窗口（View）
│   └── HistoryWindow.xaml / HistoryWindow.xaml.cs   # 历史壁纸窗口
├── Converters/                     # 绑定转换器
│   ├── BoolToVisibilityConverter.cs
│   ├── NullToVisibilityConverter.cs
│   ├── FilePathToThumbnailConverter.cs
│   ├── BingDateConverter.cs
│   └── InvertedBooleanToVisibilityConverter.cs
├── icon.ico                        # 程序图标
└── README.md / AGENTS.md           # 项目文档
```

## 3. 开发环境

### 3.1 .NET SDK

项目使用 **.NET 10 SDK**。可通过以下命令检查已安装版本：

```bash
dotnet --list-sdks
```

当前环境已安装：`10.0.301`

### 3.2 主要依赖

| 包/技术 | 用途 |
|---------|------|
|`CommunityToolkit.Mvvm` | MVVM 基础类库，提供 `ObservableObject`、`RelayCommand`、`ObservableProperty` 等源生成器支持 |
|`System.Net.Http` | HTTP 请求，获取 Bing API 数据 |
|`System.Text.Json` | JSON 序列化/反序列化 |
|`System.Windows.Media.Imaging` | WPF 图片加载与显示 |
|`Microsoft.Win32` | 注册表操作（开机启动）、文件对话框 |
|`P/Invoke (user32.dll)` | `SystemParametersInfoW` 设置桌面壁纸、`EnumDisplaySettings` 获取物理分辨率 |

## 4. 代码架构

### 4.1 应用入口 `App.xaml` / `App.xaml.cs`

- 在 `Application_Startup` 中启用 DPI 感知（`SetProcessDpiAwareness(1)`）
- 使用 `Mutex("BingWallpaperGUI_SingleInstance")` 实现单实例运行；若已有实例则直接 `Shutdown()`
- 检测命令行参数 `--auto-start`
- 非自动模式：创建并显示 `MainWindow`
- 自动模式：不创建 `MainWindow`，直接实例化 `MainViewModel` 并调用 `RunAutoStartAsync()`，完成后触发关闭

### 4.2 MVVM 架构

项目采用 **MVVM（Model-View-ViewModel）** 架构，通过 `CommunityToolkit.Mvvm` 的源生成器减少样板代码：

- **Model**：`Models/` 下的纯数据类（`WallpaperInfo`、`LocalWallpaper` 等）
- **View**：`MainWindow`、`HistoryWindow` 等 XAML 窗口，负责展示和纯视图事件转发
- **ViewModel**：`ViewModels/` 下的 `MainViewModel`、`HistoryViewModel`
  - 使用 `[ObservableProperty]` 自动生成通知属性
  - 使用 `[RelayCommand]` 自动生成命令
  - 不直接引用视图类型，通过 `IDialogService` 处理弹窗和对话框
- **Service**：`Services/` 下的业务逻辑和平台交互（网络、注册表、文件系统等）

### 4.3 核心服务 `Services/`

| 服务 | 说明 |
|------|------|
| `BingApiService.FetchWallpapersAsync()` | 调用 Bing API 获取壁纸元数据，支持重试 |
| `BingApiService.BuildImageUrl()` | 根据分辨率替换 URL 中的尺寸后缀 |
| `BingApiService.DownloadImageAsync()` | 下载图片到本地，支持重试 |
| `BingApiService.GetDefaultResolution()` | 根据主屏幕**物理分辨率**匹配最佳预设（4K 及以上返回 UHD） |
| `WallpaperService.SetDesktopWallpaper()` | 调用 `SystemParametersInfoW` 设置桌面壁纸 |
| `AutoStartService.IsAutoStartEnabled()` | 读取注册表判断是否已设置开机启动 |
| `AutoStartService.SetAutoStart()` | 写入/删除注册表实现开机启动（带 `--auto-start` 参数） |
| `DataService.GetLocalWallpapers()` | 扫描本地 `wallpapers` 目录并读取元数据 |
| `DataService.GetTodayWallpaper()` | 按日期前缀查找今日已下载壁纸 |
| `DataService.SaveMetadata()` | 将壁纸标题、版权等信息保存为 JSON |
| `DataService.DeleteWallpaper()` | 删除图片文件及对应 `.metadata/` 下 JSON |
| `Logger` | 写入 `%LOCALAPPDATA%\BingWallpaperGUI\logs\app_YYYYMMDD.log` |
| `IDialogService` / `DialogService` | 抽象 `MessageBox` 与文件对话框，便于 ViewModel 测试和替换 |

### 4.4 主窗口 `MainWindow.xaml` / `MainWindow.xaml.cs`

- **左侧**：图片预览区（`Image` 绑定到 `MainViewModel.PreviewImage`，`Stretch="Uniform"` 保持宽高比）
- **右侧**：控制面板（分辨率、地区选择、操作按钮、开机启动选项）
- **底部**：状态栏
- `MainWindow.xaml.cs` 仅负责：
  - 创建 `MainViewModel` 并设置 `DataContext`
  - 订阅 `RequestOpenHistory` / `RequestClose` 事件
  - 在窗口关闭时调用 `ViewModel.Cleanup()`
- 业务逻辑全部位于 `MainViewModel`
- 网络/文件操作在后台 `Task` 中执行，通过自动生成的通知属性更新 UI
- 窗口加载完成后调用 `ViewModel.LoadTodayWallpaperIfExists()`：如果本地已有当日壁纸，自动加载预览和元数据

### 4.5 历史记录窗口 `Views/HistoryWindow.xaml` / `HistoryWindow.xaml.cs`

- 模态对话框（`ShowDialog`）
- 使用 `ItemsControl` + `DataTemplate` 绑定到 `HistoryViewModel.Wallpapers`
- 每条记录展示缩略图、标题、版权、日期，并解析文件名显示 **分辨率** 与 **地区**
- 支持「设为壁纸」「打开文件」和「删除」，命令参数为当前 `LocalWallpaper` 项
- 删除时通过 `IDialogService.ShowQuestion` 确认，同时删除图片文件及 `.metadata/` 下 JSON
- 缩略图通过 `FilePathToThumbnailConverter` 加载，并限制 `DecodePixelWidth = 360`，避免加载大量 4K 原图导致内存压力

### 4.6 自动启动模式

程序支持 `--auto-start` 命令行参数：

- 检测到该参数时，不创建 `MainWindow`，直接实例化 `MainViewModel` 并调用 `RunAutoStartAsync()`
- `RunAutoStartAsync()` 使用 `MainViewModel.SelectedResolution`（即 `BingApiService.GetDefaultResolution()` 获取的显示器匹配分辨率）下载并设置壁纸
- 自动下载并设置壁纸，完成后触发 `RequestClose` 事件，关闭程序
- 通过注册表 `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` 实现

## 5. 构建与打包

### 5.1 运行开发版本

```bash
cd E:\MyPrograms\BingWallpaperGUI-Net10
dotnet build
dotnet run
```

### 5.2 发布为可执行文件

```bash
# 框架依赖发布（体积小，需目标机器安装 .NET 10 Windows Desktop Runtime）
dotnet publish -c Release -r win-x64 --self-contained false

# 独立发布（无需安装运行时，体积较大）
dotnet publish -c Release -r win-x64 --self-contained true
```

发布输出固定到项目根目录：

```
publish\
```

包含 `BingWallpaperGUI.exe` 及依赖。

## 6. 代码风格与约定

- **命名**：
  - 类、结构体、枚举使用 `PascalCase`
  - 方法、属性、字段使用 `PascalCase`
  - 私有字段以下划线开头（如 `_currentImagePath`）
  - 常量使用 `UPPER_SNAKE_CASE`
- **MVVM 约定**：
  - ViewModel 不直接引用 View，View 通过 `DataContext` 绑定
  - 按钮点击使用 `RelayCommand`，不再在 code-behind 中写事件处理
  - 需要弹窗/对话框时，通过注入的 `IDialogService` 调用，不要直接在 ViewModel 中调用 `MessageBox`
  - ViewModel 通过事件（如 `RequestOpenHistory`、`RequestClose`）通知 View 执行纯视图操作
- **CommunityToolkit.Mvvm**：
  - 使用 `[ObservableProperty]` 定义可通知属性，避免手写 `INotifyPropertyChanged` 代码
  - 使用 `[RelayCommand]` 定义命令，并通过 `[NotifyCanExecuteChangedFor]` 控制命令可用状态
  - 需要命令参数时，在 XAML 中设置 `CommandParameter="{Binding}"`
- **字符串**：界面文案使用中文，编码为 UTF-8
- **异常处理**：网络/文件/注册表操作均使用 `try/catch`，通过 `IDialogService` 向用户展示错误；静默吞掉的异常应通过 `Logger` 记录
- **线程安全**：后台线程通过自动属性通知或 `Dispatcher.Invoke` 回 UI 线程更新界面
- **可空引用类型**：项目已启用 `<Nullable>enable</Nullable>`，注意处理可空字符串和对象

## 7. 关键实现细节

### 7.1 数据目录定位

```csharp
public static string DataDirectory
{
    get
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);

        // 开发模式：如果 exe/dll 位于 bin/Debug|Release/... 下，且上级目录包含 .csproj，
        // 则将壁纸保存到项目根目录，与 Python 原版的开发模式行为保持一致。
        while (dir != null)
        {
            if (dir.Name.Equals("bin", StringComparison.OrdinalIgnoreCase))
            {
                var projectDir = dir.Parent;
                if (projectDir != null &&
                    Directory.EnumerateFiles(projectDir.FullName, "*.csproj").Any())
                {
                    return Path.Combine(projectDir.FullName, "wallpapers");
                }
            }
            dir = dir.Parent;
        }

        // 发布模式：使用 exe 所在目录
        return Path.Combine(baseDir, "wallpapers");
    }
}
```

- **开发模式**（`dotnet run` / Visual Studio 调试）：壁纸保存到项目根目录下的 `wallpapers/`，与 Python 原版行为一致
- **发布模式**（`dotnet publish` 后运行）：壁纸保存到 `publish/` 目录下的 `wallpapers/`，与 exe 同级

### 7.2 壁纸文件命名

下载保存的壁纸按以下格式命名：

```
{yyyyMMdd}_{标题}_{分辨率}_{地区}.jpg
```

例如：

```
20260621_一个郁郁葱葱的王国_1920x1080_zh-CN.jpg
```

- 标题经过 `BingApiService.SanitizeFileName` 处理，保留中文、字母、数字，其余字符替换为下划线
- 分辨率取自用户选择的 `SelectedResolution`（如 `1920x1080`、`UHD`）
- 地区取自 `SelectedLocale`（如 `zh-CN`、`en-US`）
- 若同文件已存在，自动追加 `_1`、`_2` 等序号后缀

`DataService.GetLocalWallpapers()` 会从文件名反向解析出分辨率与地区，供历史记录窗口展示。

### 7.3 分辨率适配

- 通过 `user32.dll!EnumDisplaySettings` 获取主屏幕**物理分辨率**（而非 WPF 设备无关单位）
- 4K 及以上屏幕（面积 >= 3840×2160）默认选择 `UHD`
- 其他屏幕按「宽高比优先、面积次优」的原则从预设列表中匹配最接近的分辨率
- 自动启动模式下同样使用 `GetDefaultResolution()` 的结果，避免下载和显示器不匹配的分辨率
- URL 分辨率替换逻辑：将 `_1920x1080` 替换为 `_UHD` 或 `_{resolution}`

### 7.4 DPI 感知

项目通过 `app.manifest` 声明 `PerMonitorV2` 与 `system` DPI 感知，并在 `App.xaml.cs` 中保留 `SetProcessDpiAwareness` / `SetProcessDPIAware` 作为旧系统 fallback：

```xml
<!-- app.manifest -->
<windowsSettings>
  <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
  <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2, system</dpiAwareness>
</windowsSettings>
```

## 8. 修改注意事项

1. **不要修改 `icon.ico` 文件名** — `BingWallpaperWPF.csproj` 中通过 `<ApplicationIcon>icon.ico</ApplicationIcon>` 引用
2. **新增依赖时**：在 `BingWallpaperWPF.csproj` 中添加 `<PackageReference>`，并确保开发环境可还原
3. **注册表操作**：`AutoStartService.SetAutoStart` 涉及系统注册表，需以普通用户权限运行即可（写入 `HKEY_CURRENT_USER`）
4. **壁纸存储**：下载的壁纸和 `.metadata/` 目录与 `exe` 同级，重装系统或移动程序时数据不会丢失（只要复制整个文件夹）
5. **单实例约束**：已通过 `Mutex("BingWallpaperGUI_SingleInstance")` 在 `App.xaml.cs` 中实现。检测到已有实例时直接 `Shutdown()`
6. **WPF 线程**：所有 UI 更新必须在 UI 线程执行；后台服务类本身不持有 `Dispatcher`，由 ViewModel 在必要时通过 `Dispatcher` 调度
7. **MVVM 修改**：新增 UI 交互时，优先在 ViewModel 中写 `RelayCommand`，避免在 code-behind 中添加新的事件处理方法

## 9. 常见问题排查

| 问题 | 可能原因 | 解决方向 |
|------|---------|---------|
| 图片无法显示 | 文件路径错误或图片损坏 | 检查 `DataService.DataDirectory` 与文件是否存在 |
| 开机启动不生效 | 路径含中文或空格未加引号 | 检查注册表值是否带 `""` 包裹 |
| 自动模式窗口闪退 | 网络异常未捕获 | 检查 `MainViewModel.RunAutoStartAsync` 的异常处理逻辑；查看 `%LOCALAPPDATA%\BingWallpaperGUI\logs` 日志 |
| 高分辨率屏幕 UI 模糊 | DPI 感知设置失败 | 检查 `app.manifest` 与 `SetProcessDpiAwareness` 调用 |
| 构建失败 `Application` 不明确 | 同时引用了 WinForms | 检查 `csproj` 是否包含 `<UseWindowsForms>true</UseWindowsForms>` |
| 按钮命令不触发或 CanExecute 不更新 | `[NotifyCanExecuteChangedFor]` 缺失或命令参数类型不匹配 | 检查依赖属性的通知声明与 XAML 中的 `CommandParameter` |
| ViewModel 中无法使用 `MessageBox` | MVVM 要求 ViewModel 不依赖视图 | 通过 `IDialogService` 抽象，或在 code-behind 中响应 ViewModel 事件 |
| 发布文件过大 | 默认包含完整运行时 | 使用 `--self-contained false` 或启用裁剪（Trimming） |
