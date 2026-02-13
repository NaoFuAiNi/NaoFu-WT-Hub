# NaoFu WT Font 2.11 - 发布版构建脚本
# 脚本在 debug\scripts 下，项目根 = 上两级。在项目根执行: .\debug\scripts\build_release.ps1 或在 debug 下: .\scripts\build_release.ps1
$ErrorActionPreference = "Stop"
$root = if ($PSScriptRoot) { Split-Path -Parent (Split-Path -Parent $PSScriptRoot) } else { Split-Path -Parent (Split-Path -Parent (Get-Location | Select-Object -ExpandProperty Path)) }
$debug = Join-Path $root "debug"
$release = Join-Path $root "release"

if (-not (Test-Path $debug)) { Write-Error "未找到 debug 目录：$debug" }

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

# 删除 bin/ui 中的内部文档（若存在），避免泄露
$binUiDir = Join-Path $publishOut "ui"
if ($binUiDir -and (Test-Path -LiteralPath $binUiDir)) {
    Get-ChildItem -Path $binUiDir -Filter "*.md" -File -ErrorAction SilentlyContinue | Remove-Item -Force
}
Write-Host "复制 tools ..."
Copy-Item (Join-Path $debug "NaoFu WT Customize Font 2.11.exe") (Join-Path $release "tools") -Force
if (Test-Path (Join-Path $debug "nf_subset_tool.exe")) {
    Copy-Item (Join-Path $debug "nf_subset_tool.exe") (Join-Path $release "tools") -Force
} else {
    Write-Warning "nf_subset_tool.exe not found in debug root; skipping copy (font subsetting will be unavailable in this release)."
}

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

Write-Host "复制 ui、assets 到 release\bin ..."
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

Write-Host "复制 font 到 release\font ..."
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

Write-Host 'Done. Pack the release folder for distribution.'
Write-Host 'Entry exe: release\NaoFu WT Hub.exe'
