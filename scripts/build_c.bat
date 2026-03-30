@echo off
chcp 65001 >nul
REM 自动查找 VS 并编译 C 主程序。在 debug 目录执行: scripts\build_c.bat
cd /d "%~dp0.."

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
set "VSINSTALL="
if exist "%VSWHERE%" for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath 2^>nul`) do set "VSINSTALL=%%i"
if not defined VSINSTALL set "VSINSTALL=C:\Program Files\Microsoft Visual Studio\2022\Community"
if not exist "%VSINSTALL%\VC\Auxiliary\Build\vcvars64.bat" (
    echo [ERROR] Visual Studio not found. Please install VS with "Desktop development with C++" workload.
    echo         Or set VSINSTALL in this script to your VC\Auxiliary\Build parent path.
    exit /b 1
)
call "%VSINSTALL%\VC\Auxiliary\Build\vcvars64.bat" >nul 2>&1

if not exist "src\obj" mkdir "src\obj"
cl /nologo /W3 /O2 /utf-8 /I src\include /Fo"src\obj\\" /Fe"NaoFu_WT_Customize_Font_2.1.3_new.exe" src\source\main.c src\source\nf_bin.c src\source\nf_console.c src\source\nf_fonts.c src\source\nf_io.c src\source\nf_patcher.c src\source\nf_subset.c src\source\nf_ui.c
if errorlevel 1 (echo BUILD FAILED & exit /b 1)
copy /Y "NaoFu_WT_Customize_Font_2.1.3_new.exe" "NaoFu WT Customize Font 2.1.3.exe" >nul 2>&1
if exist "NaoFu_WT_Customize_Font_2.1.3_new.exe" del "NaoFu_WT_Customize_Font_2.1.3_new.exe"
echo BUILD OK: NaoFu WT Customize Font 2.1.3.exe
