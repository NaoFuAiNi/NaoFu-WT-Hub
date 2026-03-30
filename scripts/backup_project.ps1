# NaoFu WT Font 2.1.3 项目完整备份脚本
# 将整个项目（debug 的父目录）打包为 zip，保存到项目上级目录，文件名含日期
# 在项目根执行: .\debug\scripts\backup_project.ps1 或在 debug 下: .\scripts\backup_project.ps1

$ErrorActionPreference = "Stop"
$ProjectRoot = if ($PSScriptRoot) { Split-Path -Parent (Split-Path -Parent $PSScriptRoot) } else { Split-Path -Parent (Split-Path -Parent (Get-Location | Select-Object -ExpandProperty Path)) }
$ParentDir = Split-Path -Parent $ProjectRoot
$TimeStamp = Get-Date -Format "yyyyMMdd_HHmm"
$ZipName = "NaoFu_WT_Font_2.1.3_备份_$TimeStamp.zip"
$ZipPath = Join-Path $ParentDir $ZipName

Write-Host "正在备份项目到: $ZipPath" -ForegroundColor Cyan
# 使用 .NET 压缩，避免 Compress-Archive 路径过长问题
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($ProjectRoot, $ZipPath, [System.IO.Compression.CompressionLevel]::Optimal, $true)
Write-Host "备份完成: $ZipPath" -ForegroundColor Green
$Size = (Get-Item $ZipPath).Length / 1MB
Write-Host "大小: $([math]::Round($Size, 2)) MB" -ForegroundColor Gray
