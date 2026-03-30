# NaoFu WT Font 2.1.3 - Debug build (C then Launcher)
# Run from repo root: .\debug\scripts\build_debug.ps1  or from debug: .\scripts\build_debug.ps1
$ErrorActionPreference = "Stop"
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$debug = Split-Path -Parent $scriptDir
$root = Split-Path -Parent $debug
if (-not (Test-Path (Join-Path $debug "Launcher"))) {
    $debug = Join-Path $root "debug"
}
if (-not (Test-Path (Join-Path $debug "Launcher\NaoFu.WT.Font.Launcher.csproj"))) {
    Write-Error "Launcher project not found. Run from repo root or debug dir."
}

Write-Host "=== 1/3 Building C (NaoFu WT Customize Font 2.1.3.exe) ===" -ForegroundColor Cyan
Push-Location $debug
try {
    & cmd /c "scripts\build_c.bat"
    if ($LASTEXITCODE -ne 0) { throw "C build failed" }
}
finally {
    Pop-Location
}

$subsetExe = Join-Path $debug "nf_subset_tool.exe"
$subsetToolDir = Join-Path $debug "tools\nf_subset_tool"
if (-not (Test-Path $subsetExe) -and (Test-Path (Join-Path $subsetToolDir "subset_tool.py"))) {
    Write-Host ""
    Write-Host "=== 2/3 Building nf_subset_tool.exe (optional, for font subsetting) ===" -ForegroundColor Cyan
    $py = Get-Command python -ErrorAction SilentlyContinue; if (-not $py) { $py = Get-Command py -ErrorAction SilentlyContinue }
    if ($py) {
        Push-Location $subsetToolDir
        try {
            & $py.Source -m pip install -r requirements.txt -q 2>$null
            & $py.Source -m PyInstaller --onefile --noconsole --name nf_subset_tool subset_tool.py 2>$null
            $distExe = Join-Path $subsetToolDir "dist\nf_subset_tool.exe"
            if (Test-Path $distExe) {
                Copy-Item $distExe $subsetExe -Force
                Write-Host "nf_subset_tool.exe built and copied to debug root." -ForegroundColor Green
            }
        }
        catch { Write-Host "nf_subset_tool build skipped or failed (font subsetting may be unavailable)." -ForegroundColor Yellow }
        finally { Pop-Location }
    }
    else { Write-Host "Python not found; nf_subset_tool.exe skipped. Run tools\nf_subset_tool\build_exe.bat manually if needed." -ForegroundColor Yellow }
}
else { Write-Host ""; Write-Host "=== 2/3 nf_subset_tool.exe already present, skip. ===" -ForegroundColor Gray }

Write-Host ""
Write-Host "=== 3/3 Building Launcher (Debug) ===" -ForegroundColor Cyan
$launcherProj = Join-Path $debug "Launcher\NaoFu.WT.Font.Launcher.csproj"
& dotnet build $launcherProj -c Debug
if ($LASTEXITCODE -ne 0) { throw "Launcher build failed" }

Write-Host ""
$outDir = Join-Path $debug "Launcher\bin\Debug\net8.0-windows"
if ((Test-Path $subsetExe) -and (Test-Path $outDir)) {
    Copy-Item $subsetExe (Join-Path $outDir "nf_subset_tool.exe") -Force
}
Write-Host "Done. Run: debug\Launcher\bin\Debug\net8.0-windows\NaoFu WT Hub 2.1.3.exe" -ForegroundColor Green
