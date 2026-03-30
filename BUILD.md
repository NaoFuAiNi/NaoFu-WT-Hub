# 从零编译与发布说明（开发者 / 贡献者）

克隆仓库后想自己编 Debug 或打 Release 包的话，照着下面做就行。

---

## 一、环境要求

- **系统**：Windows 10 / 11
- **.NET 8 SDK**：用于编译 Launcher（C#）  
  下载地址：`https://dotnet.microsoft.com/download/dotnet/8.0`
- **Visual Studio 2022 / 2019 或 Build Tools**：需安装工作负载 **「使用 C++ 的桌面开发」**  
  debug 下的脚本会通过系统自带的 **vswhere** 自动查找 VS 安装路径，一般无需手动改路径。
- **Python 3**（可选）：仅当需要重新打包 **nf_subset_tool.exe** 时使用。

---

## 二、一键编译 Debug（开发与测试）

在仓库 **根目录**（与 `debug` 同级）或先进入 **debug** 目录，在 PowerShell 中执行：

```powershell
.\debug\scripts\build_debug.ps1
```

或在 debug 下：

```powershell
.\scripts\build_debug.ps1
```

脚本会依次：

1. 编译 C 主程序到 `debug\NaoFu WT Customize Font 2.1.3.exe`
2. 编译 Launcher（Debug），并将 C 程序与 `font` 等资源复制到  
   `debug\Launcher\bin\Debug\net8.0-windows\`

完成后运行：

```
debug\Launcher\bin\Debug\net8.0-windows\NaoFu WT Hub 2.1.3.exe
```

即可打开完整工具界面（包括「自定义字体」等功能）。

若希望分步执行或仅编译其中一部分，可参阅 `debug/scripts/README.md`，其中包含各脚本的用途与用法（仅编译 C：`scripts\build_c.bat`；生成 Release：`scripts\build_release.ps1` 等）。

---

## 三、一键生成 Release 目录（发布给用户）

当你需要打包一个可发给别人的「成品」版本时，可使用 `build_release.ps1`。

在**项目根目录**下执行（PowerShell）：

```powershell
.\debug\scripts\build_release.ps1
```

或在 **debug** 目录下执行：

```powershell
.\scripts\build_release.ps1
```

脚本会：

1. 清空并创建项目根下的 **release** 目录
2. 发布 Launcher（Release、win-x64、自包含）到 `release/bin`
3. 从 `debug` 复制 **NaoFu WT Customize Font 2.1.3.exe**、**nf_subset_tool.exe** 到 `release/tools`
4. 复制 `ui`、`assets`、`font` 等（排除内部 .md）到对应位置
5. 复制根目录启动器 **NaoFu WT Hub.exe** 到 `release` 根目录
6. 将 `debug/README.md` 复制为 `release/README.md`（给最终用户看的说明）

完成后，将 **release** 文件夹整体压缩打包即可分发。  
用户解压后双击 `release/NaoFu WT Hub.exe` 即可运行。

---

## 四、Release 前需要先编译好的内容

`build_release.ps1` 会从 `debug` 目录复制以下文件，请先确保它们已存在且为最新：

| 文件 | 如何生成 |
|------|----------|
| **NaoFu WT Customize Font 2.1.3.exe** | 在 `debug` 下执行 `scripts\build_c.bat`（推荐）或 `scripts\do_build.cmd`，也可在「x64 本机工具命令提示」里用 `scripts\build.bat` |
| **NaoFu WT Hub.exe** | 在 `debug` 下执行 `scripts\build_launcher_stub.bat` |
| **Launcher（Release）** | 不必手动单独发布，`build_release.ps1` 内会调用 `dotnet publish`；若仅想预先编译，可在项目根或 debug 下执行 `dotnet build debug\Launcher\NaoFu.WT.Font.Launcher.csproj -c Release` |
| **nf_subset_tool.exe** | 进入 `debug/tools/nf_subset_tool`，执行 `build_exe.bat`，再将 `dist/nf_subset_tool.exe` 复制到 `debug` 根目录 |

---

## 五、可选：发布前清理

想让 release 包干净点的话，可以先清一下 debug 里的编译产物和用户数据（不必须，脚本会按现有内容复制）：

- **Launcher**：可先删 `debug/Launcher/obj`、`debug/Launcher/bin`，再重新执行 `build_release.ps1`（内部会重新 publish）
- **build/** 子目录、`font/builtin/` 下内容、`font/custom/` 下文件、`nf_config.json`：为用户使用后产生，删除后 release 中对应目录为空，用户首次使用无影响
- **.obj 文件**：C 编译中间文件，可删，下次执行 `build.bat` / `build_c.bat` 会重新生成

---

## 六、release 与 Release 目录说明

- 脚本生成的目录名为 **release**（小写），位于项目根目录下
- 若你已有 **Release**（大写）目录，两者不同；脚本不会修改 **Release**，只维护 **release**
- 打包分发时，将 **release** 整个压缩为 zip，或重命名为 **Release** 均可

---

## 七、常见问题

- **提示「Visual Studio not found」**  
  请安装 Visual Studio 或 Build Tools，并勾选「使用 C++ 的桌面开发」工作负载。  
  若安装在非默认路径，可在 `debug/scripts` 下的 C 编译脚本中修改 `VSINSTALL` 的 fallback 值。

- **提示「未找到 C 程序所在目录」**  
  说明 Launcher 输出目录下没有 C 主程序 exe。  
  请先执行 `build_debug.ps1` 或至少执行一次 `scripts\build_c.bat` 生成 `NaoFu WT Customize Font 2.1.3.exe`，  
  然后重新编译 Launcher，或将该 exe 复制到 `Launcher\bin\Debug\net8.0-windows\`（或对应 Release 输出目录）。

- **nf_subset_tool.exe 相关**  
  字体瘦身功能依赖此 exe。若需自行打包，见 `tools/nf_subset_tool/build_exe.bat`；  
  否则可从已有发布包中复制到 debug 根目录或 Launcher 输出目录。
