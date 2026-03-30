# NaoFu WT Font 2.1.3 - 发布版构建脚本
# 脚本在 debug\scripts 下，项目根 = 上两级。在项目根执行: .\debug\scripts\build_release.ps1 或在 debug 下: .\scripts\build_release.ps1
$ErrorActionPreference = "Stop"
$root = if ($PSScriptRoot) { Split-Path -Parent (Split-Path -Parent $PSScriptRoot) } else { Split-Path -Parent (Split-Path -Parent (Get-Location | Select-Object -ExpandProperty Path)) }
$debug = Join-Path $root "debug"
$release = Join-Path $root "release"

if (-not (Test-Path $debug)) { Write-Error "未找到 debug 目录：$debug" }

function Ensure-NfSubsetToolInDebug {
    param([Parameter(Mandatory)][string]$DebugRoot)
    $subsetExe = Join-Path $DebugRoot "nf_subset_tool.exe"
    if (Test-Path $subsetExe) { return }
    $subsetToolDir = Join-Path (Join-Path $DebugRoot "tools") "nf_subset_tool"
    $pyFile = Join-Path $subsetToolDir "subset_tool.py"
    if (-not (Test-Path $pyFile)) {
        throw 'Missing debug/nf_subset_tool.exe: subset_tool.py not found under tools/nf_subset_tool. Run build_exe.bat there and copy dist/nf_subset_tool.exe to debug root.'
    }
    $py = Get-Command python -ErrorAction SilentlyContinue
    if (-not $py) { $py = Get-Command py -ErrorAction SilentlyContinue }
    if (-not $py) {
        throw 'Missing nf_subset_tool.exe and python/py on PATH. Install Python 3 or copy nf_subset_tool.exe to debug root.'
    }
    Write-Host "自动打包 nf_subset_tool.exe (PyInstaller) ..."
    Push-Location $subsetToolDir
    try {
        & $py.Source -m pip install -r requirements.txt -q
        if ($LASTEXITCODE -ne 0) { throw "pip install -r requirements.txt 失败" }
        & $py.Source -m PyInstaller --onefile --noconsole --name nf_subset_tool subset_tool.py
        if ($LASTEXITCODE -ne 0) { throw "PyInstaller 打包 nf_subset_tool 失败" }
        $distExe = Join-Path (Join-Path $subsetToolDir "dist") "nf_subset_tool.exe"
        if (-not (Test-Path $distExe)) { throw 'PyInstaller did not produce dist/nf_subset_tool.exe' }
        Copy-Item -LiteralPath $distExe -Destination $subsetExe -Force
        Write-Host "已生成: $subsetExe" -ForegroundColor Green
    }
    finally { Pop-Location }
}

Write-Host "清理 release ..."
if (Test-Path $release) {
    Get-ChildItem $release -Force | Remove-Item -Recurse -Force
}
$null = New-Item -ItemType Directory -Path $release -Force
$dirs = @("bin", "tools", "font", "build")
foreach ($d in $dirs) {
    $null = New-Item -ItemType Directory -Path (Join-Path $release $d) -Force
}

Write-Host "发布 Launcher (Release, win-x64, 自包含) ..."
$launcherProj = Join-Path (Join-Path $debug "Launcher") "NaoFu.WT.Font.Launcher.csproj"
$publishOut = Join-Path $release "bin"
& dotnet publish $launcherProj -c Release -r win-x64 --self-contained true -o $publishOut -p:PublishSingleFile=false
if ($LASTEXITCODE -ne 0) { throw "Launcher 发布失败" }

Write-Host "Ensure nf_subset_tool.exe exists in debug (will copy to release/tools)..."
Ensure-NfSubsetToolInDebug -DebugRoot $debug

$cExe = Join-Path $debug "NaoFu WT Customize Font 2.1.3.exe"
if (-not (Test-Path $cExe)) {
    throw "C program not found: $cExe. Run debug\scripts\build_c.bat then re-run this script."
}

# 删除 bin/ui 中的内部文档（若存在），避免泄露
$binUiDir = Join-Path $publishOut "ui"
if ($binUiDir -and (Test-Path -LiteralPath $binUiDir)) {
    Get-ChildItem -Path $binUiDir -Filter "*.md" -File -ErrorAction SilentlyContinue | Remove-Item -Force
}
Write-Host "Copy tools (C exe + nf_subset_tool) ..."
Copy-Item $cExe (Join-Path $release "tools") -Force
Copy-Item (Join-Path $debug "nf_subset_tool.exe") (Join-Path $release "tools") -Force

# 精简发布体积：仅保留必要语言资源 & 删除 pdb
Write-Host "清理多余语言资源与调试符号 ..."
$cultureKeep = @('en', 'en-US', 'zh-Hans')
$cultureAll  = @('cs','de','es','fr','it','ja','ko','pl','pt-BR','ru','tr','zh-Hant','zh-Hans','en','en-US')
Get-ChildItem $publishOut -Directory -ErrorAction SilentlyContinue |
    Where-Object { $cultureAll -contains $_.Name -and ($cultureKeep -notcontains $_.Name) } |
    ForEach-Object {
        try {
            Remove-Item $_.FullName -Recurse -Force -ErrorAction Stop
        } catch { Write-Warning "删除语言目录失败: $($_.FullName) - $($_.Exception.Message)" }
    }

Get-ChildItem $release -Recurse -Filter *.pdb -ErrorAction SilentlyContinue |
    ForEach-Object {
        try {
            Remove-Item $_.FullName -Force -ErrorAction Stop
        } catch { Write-Warning "删除 pdb 失败: $($_.FullName) - $($_.Exception.Message)" }
    }

Write-Host 'Copy ui, assets to release\bin ...'
foreach ($sub in @("ui", "assets")) {
    $src = Join-Path $debug $sub
    $dst = Join-Path $publishOut $sub
    if (Test-Path $src) {
        if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }
        Copy-Item -Path $src -Destination $dst -Recurse -Force
    }
}
Get-ChildItem -Path (Join-Path $publishOut "ui") -Filter "*.md" -File -ErrorAction SilentlyContinue | Remove-Item -Force
$binAssetsDir = Join-Path $publishOut "assets"
if ($binAssetsDir -and (Test-Path $binAssetsDir)) { Get-ChildItem $binAssetsDir -Filter "*.md" -File -ErrorAction SilentlyContinue | Remove-Item -Force }

Write-Host 'Copy font to release\font ...'
$fontSrc = Join-Path $debug "font"
$fontDst = Join-Path $release "font"
if (Test-Path $fontSrc) {
    Get-ChildItem $fontSrc -Recurse | ForEach-Object {
        $rel = $_.FullName.Substring($fontSrc.Length).TrimStart("\")
        $target = Join-Path $fontDst $rel
        if ($_.PSIsContainer) { $null = New-Item -ItemType Directory -Path $target -Force }
        else { Copy-Item $_.FullName $target -Force }
    }
}

Write-Host "复制根目录启动器 ..."
Copy-Item (Join-Path $debug "NaoFu WT Hub.exe") (Join-Path $release "NaoFu WT Hub.exe") -Force

# 用户向 README（使用 debug/README.md 作为发布包说明）
$readmeSrc = Join-Path $debug "README.md"
if (Test-Path $readmeSrc) {
    Copy-Item $readmeSrc (Join-Path $release "README.md") -Force
} else {
    Write-Warning 'debug/README.md not found.'
}

Write-Host "Done. Pack the release folder for distribution."
Write-Host "Entry exe: release\NaoFu WT Hub.exe"
