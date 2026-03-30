# 构建与维护脚本说明

debug 用到的脚本都放这儿，`.ps1`、`.bat`、`.cmd` 之类。

## 前置条件（克隆仓库后从零编译）

- **.NET 8 SDK**：用于编译 Launcher（C#）
- **Visual Studio**（含「使用 C++ 的桌面开发」工作负载）或 **Build Tools**：用于编译 C 主程序与根目录启动器。脚本会通过 **vswhere** 自动查找本机 VS 安装路径，无需手动改路径。
- **Python 3**（可选）：仅当需要重新打包 **nf_subset_tool.exe** 时使用。

## 一键编译 Debug（推荐）

在**项目根目录**或 **debug** 目录下执行（PowerShell）：

```powershell
.\debug\scripts\build_debug.ps1
```

或在 debug 下：

```powershell
.\scripts\build_debug.ps1
```

会依次：1）编译 C 主程序到 `debug\NaoFu WT Customize Font 2.1.3.exe`；2）编译 Launcher (Debug)，并将 C 程序与 font 等复制到 `Launcher\bin\Debug\net8.0-windows\`。完成后直接运行该目录下的 **NaoFu WT Hub 2.1.3** 即可。

## 脚本一览

| 脚本 | 用途 | 运行方式 |
|------|------|----------|
| **build_debug.ps1** | 一键编译 Debug：先 C 主程序，再 Launcher，输出可直接运行 | 项目根或 debug 下：`.\debug\scripts\build_debug.ps1` 或 `.\scripts\build_debug.ps1` |
| **build_release.ps1** | 生成发布版：清理并生成项目根下的 `release` 目录 | 项目根或 debug 下：`.\debug\scripts\build_release.ps1` 或 `.\scripts\build_release.ps1` |
| **backup_project.ps1** | 将整个项目打包为 zip 备份到项目上级目录，文件名含日期 | 同上 |
| **build_c.bat** / **do_build.cmd** | 编译 C 主程序。自动通过 vswhere 查找 VS，无需改路径 | 在 **debug** 目录执行：`scripts\build_c.bat` 或 `scripts\do_build.cmd`（普通 cmd 或 PowerShell 均可） |
| **build.bat** | 仅编译 C 主程序，不设置环境，需在「x64 本机工具命令提示」下运行 | 在 **debug** 目录执行：`scripts\build.bat` |
| **build_launcher_stub.bat** | 编译根目录用启动器 NaoFu WT Hub.exe。自动查找 VS | 在 **debug** 目录执行：`scripts\build_launcher_stub.bat` |

**说明：**

- C 相关脚本会先 `cd` 到脚本所在目录的上一级（即 debug 目录），因此请在 **debug** 目录下运行（例如 `scripts\build_c.bat`），不要进入 `scripts` 后单独双击脚本（否则工作目录会错）。
- 若本机未安装 VS 或未安装 C++ 工作负载，脚本会提示安装「使用 C++ 的桌面开发」。若 VS 安装在非默认路径，可设置环境变量 **VSINSTALL** 或在脚本内修改 `VSINSTALL` 的 fallback 值。
- **nf_subset_tool** 的打包脚本在 `debug\tools\nf_subset_tool\build_exe.bat`，需在该目录下执行 pip/pyinstaller；生成后将 `dist\nf_subset_tool.exe` 复制到 debug 根目录即可。
