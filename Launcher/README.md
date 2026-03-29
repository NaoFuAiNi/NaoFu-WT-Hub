# Launcher

Windows 桌面启动器（WinForms, .NET 8）。用 WebView2 载 `ui/` 的页面，和 C 程序、nf_subset_tool 通信，管文件选择、最近字体、主题这些。

## Build

- 使用脚本：在仓库根或 `debug` 下执行 `debug/scripts/build_debug.ps1`（推荐）。  
- 或单独编译：在仓库根或 `debug` 下执行  
  `dotnet build debug/Launcher/NaoFu.WT.Font.Launcher.csproj -c Debug`。

Debug 构建后，`Launcher/bin/Debug/net8.0-windows/` 目录可直接运行 `NaoFu WT Hub 2.1.2.exe`。

