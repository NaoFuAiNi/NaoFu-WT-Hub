# tools/nf_subset_tool

字体**子集化 + 西里尔→拉丁映射**的独立小工具，供 C 主程序调用。

## 组成

- `subset_tool.py`：核心逻辑（使用 `fontTools` 提供的 `TTFont` 与 `subset` 模块）。  
  - 支持两种用法：  
    - `subset_tool.py <input_font> <output_font>`：仅做西里尔 `Т/У/Е` → 拉丁 `T/U/E` 映射；  
    - `subset_tool.py <ref_font> <input_font> <output_font>`：按参考字体字符集做子集化，并附带映射。  
- `build_exe.bat`：使用 PyInstaller 打包为 `nf_subset_tool.exe`。

## 瘦身策略（三参数模式）

三参数模式下采用**两轮瘦身**，自动适配大小迥异的槽位：

| 轮次 | 字符集 | 压缩手段 | 触发条件 |
|------|--------|----------|----------|
| **第一轮** | 与参考字体字符集完全一致 | 深度压缩（去 hinting、去 layout features）+ 可变字体压平 | 总是执行 |
| **第二轮** | 最小拉丁集（Basic Latin U+0020–U+007E + Latin-1 Supplement U+00A0–U+00FF，约 190 字）| 同上 | 第一轮结果 > 参考字体文件大小时自动触发 |

最终写入输出的是两轮中**更小**的那个。

这一策略主要解决「完整覆盖」模式下槽位 1（`default_normal.otf`，53 KB OTF 格式）难以容纳任何 TTF 字体的问题——第二轮只保留约 190 个最基础的拉丁字符，生成体积通常在 15 KB 以下，确保所有槽位都能完成替换。

## 打包为 exe

在本目录执行（需要 Python 3 + pip）：  

```bat
build_exe.bat
```

或等价命令：

```bat
pip install -r requirements.txt
pyinstaller --onefile --noconsole --name nf_subset_tool subset_tool.py
```

生成的 `dist/nf_subset_tool.exe` 拷到：

- 开发：`debug/` 根目录，这样跑 `build_debug.ps1` 或编 Launcher 时会自动带过去；  
- 发布：和 `NaoFu WT Customize Font 2.1.2.exe` 放一起，或丢发布包的 `tools/` 里。

