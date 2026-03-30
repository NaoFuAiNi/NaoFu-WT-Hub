# Resource Overview

项目里各资源目录是干啥的，简单记一笔。

## 0. Global config (app root)

- **Path**: `应用根目录/nf_config.json`（开发时为 `debug` 根目录，发布后为解压包根目录）  
- 程序首次运行或用户进行操作后自动生成，包含：游戏根路径、上次选择的字体路径、最近使用的字体列表、界面主题等。  
- 就一个 JSON 文件，备份也方便；发布包里可以不带，用户一用就会自己生成。

## 1. `assets/` (UI & brand assets)

- **Dev path**: `debug/assets/`  
- **Release path**: `bin/assets/` 或发布包根目录下的 `assets/`

放界面和品牌相关的东西：

| File | Description |
|------|-------------|
| `CuteFount.ttf` | 站酷快乐体，项目全局 UI 字体，标题栏文字优先使用。分发给用户时请保留 `assets`，否则界面会回退到系统默认字体。 |
| `app_ico.png` / `app-icon.png` | 标题栏左上角 logo，任选其一即可（优先 `app_ico.png`）。 |
| `author_avatar.jpg` | 「关于作者」页与感谢名单中的作者头像，需自行放入；若不存在，关于页会隐藏头像。 |

## 2. `font/source` (source font resources)

- **Path**: `debug/font/source/`

- `fonts.vromfs.bin`：官方字体包（或与当前游戏版本对应的 bin），C 程序读取并在此基础上做替换。  
- `fonts.vromfs.bin_u/`：上述 bin 解包后的字体文件，用于「按文件名定位」并替换。  
- `src_font_bin/`：未修改的源 bin 备份（可选），定位失败时可用作重试源。

别删或乱换，不然做不了。

## 3. `font/builtin` (built‑in & imported fonts)

- **Path**: `debug/font/builtin/`

| Subfolder | Description |
|----------|-------------|
| `系统自带/` | 软件内置字体（如也子工厂、奶酪体、爱点风雅黑），每项一个子目录，内含 `fonts.vromfs.bin` 与 `DATA.NaoFu`。 |
| `导入/` | 用户通过「导入 .bin」或「导入文件夹」添加的字体，同样按名称子目录存放。 |

首次运行或发布前可保留空目录，程序会自动创建。

## 4. `font/custom` (current working font)

- **Path**: `debug/font/custom/`

- 用户通过界面选择或拖入的字体会被复制为 `MyFonts.ttf`。  
- C 程序读取此文件进行替换与瘦身（若需要）。

发布或备份时可清空此目录内容，保留空目录即可。

## 5. `build/` (output)

- **Path**: `debug/build/`

- 每次「开始制作」会在其下按**项目名**创建子目录。  
- 子目录内包含：`fonts.vromfs.bin`、`DATA.NaoFu`、`MyFonts.ttf`（或 `MyFonts_slim.ttf`）等。  
- 用户可将该目录打包发给他人，或在本工具内「导入到游戏 UI 文件夹」。

## 6. `tools/nf_subset_tool` (subset tool)

- **Path**: `debug/tools/nf_subset_tool/`

- `subset_tool.py`：字体子集化与西里尔映射的 Python 脚本，采用**两轮瘦身策略**：  
  - **第一轮**：按参考字体字符集裁剪 + 深度压缩（去 hinting / layout features）+ 可变字体（VF）压平；  
  - **第二轮**（仅在第一轮结果仍超出参考字体大小时触发）：改用最小拉丁集（Basic Latin + Latin-1 Supplement，约 190 字）从原始字体裁剪，确保极小槽位（如 53 KB 的 OTF 槽位）也能完成替换；  
  - 最终写入两轮中体积更小的那个。  
- `build_exe.bat`：使用 PyInstaller 打包为 `nf_subset_tool.exe`，供 C 程序调用。  
- 发布时需将 `nf_subset_tool.exe` 放在与 `NaoFu WT Customize Font 2.1.3.exe` 同目录（或发布包的 `tools` 下）。

---

分发给别人时：请整包分发（保留 `assets`、`font` 等目录结构），否则界面或制作功能会异常。

