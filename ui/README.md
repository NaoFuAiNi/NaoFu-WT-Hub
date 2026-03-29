# ui/

HTML + CSS + JS，Launcher 用 WebView2 载。

## 主要页面

- `welcome.html`：启动器首页，功能入口与主题切换。  
- `customize_font.html`：自定义字体制作页面（亮/暗双主题合并版）。  
- 其他页面：关于作者、帮助说明等。

## 开发说明

- 调试：从 `Launcher/bin/Debug/net8.0-windows/` 跑 NaoFu WT Hub 2.1.2，会载本目录页面。  
- 改完 HTML/CSS/JS 刷新就行，不用重编 C。  
- 打 Release 包时 `build_release.ps1` 会把 ui 拷进发布包。

