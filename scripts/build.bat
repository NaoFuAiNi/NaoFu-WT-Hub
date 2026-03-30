@echo off
chcp 65001 >nul
REM 在「x64 本机工具命令提示」下运行。在 debug 目录执行: scripts\build.bat
cd /d "%~dp0.."
if not exist "src\obj" mkdir "src\obj"
cl /nologo /W3 /O2 /utf-8 /I src\include /Fo"src\obj\\" /Fe"NaoFu_WT_Customize_Font_2.1.3_new.exe" src\source\main.c src\source\nf_bin.c src\source\nf_console.c src\source\nf_fonts.c src\source\nf_io.c src\source\nf_patcher.c src\source\nf_subset.c src\source\nf_ui.c
if errorlevel 1 (echo 编译失败 & exit /b 1)
copy /Y "NaoFu_WT_Customize_Font_2.1.3_new.exe" "NaoFu WT Customize Font 2.1.3.exe" >nul 2>&1 && del "NaoFu_WT_Customize_Font_2.1.3_new.exe"
echo 构建成功: NaoFu WT Customize Font 2.1.3.exe
