# src/

这里是 **C 核心程序** 源码目录，对应生成的 `NaoFu WT Customize Font 2.1.2.exe`。

- `source/`：`main.c`、`nf_*.c` 等。  
- `include/`：头文件。  

主要几块：`main.c` 入口和 Launcher 桥接，`nf_patcher.c` 替换逻辑，`nf_subset.c` 调瘦身工具，`nf_ui.c` 命令行提示。  

## 编译方式

通常不单独在此目录下编译，而是使用 `debug/scripts` 中的脚本：

- 一键 Debug：`debug/scripts/build_debug.ps1`（推荐）。  
- 仅编译 C 主程序：在 `debug` 目录执行 `scripts/build_c.bat` 或 `scripts/do_build.cmd`。

