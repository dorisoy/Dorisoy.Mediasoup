# FFmpeg 库下载脚本
# 此脚本用于下载 FFmpeg 预编译库供 Dorisoy.Meeting.Client 使用

$ErrorActionPreference = "Stop"

# FFmpeg 版本 - 对应 FFmpeg.AutoGen 6.0.0
$ffmpegVersion = "6.0"
$downloadUrl = "https://github.com/GyanD/codexffmpeg/releases/download/6.0/ffmpeg-6.0-full_build-shared.zip"
$alternativeUrl = "https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-6.0-full_build-shared.zip"

# 目标目录
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputDir = Join-Path $scriptPath "bin\Debug\net8.0-windows10.0.17763.0"
$tempDir = Join-Path $env:TEMP "ffmpeg_download"
$zipFile = Join-Path $tempDir "ffmpeg.zip"

Write-Host "=== FFmpeg Library Downloader ===" -ForegroundColor Cyan
Write-Host "Version: $ffmpegVersion"
Write-Host "Output: $outputDir"
Write-Host ""

# 创建临时目录
if (-not (Test-Path $tempDir)) {
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
}

# 创建输出目录
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# 检查是否已存在 FFmpeg 库
$existingLib = Join-Path $outputDir "avcodec-60.dll"
if (Test-Path $existingLib) {
    Write-Host "FFmpeg libraries already exist in output directory." -ForegroundColor Green
    Write-Host "If you want to re-download, delete the existing DLLs first."
    exit 0
}

# 下载 FFmpeg
Write-Host "Downloading FFmpeg $ffmpegVersion..." -ForegroundColor Yellow

try {
    # 优先使用 GitHub
    Write-Host "Trying GitHub release..."
    Invoke-WebRequest -Uri $downloadUrl -OutFile $zipFile -UseBasicParsing
} catch {
    Write-Host "GitHub download failed, trying alternative source..." -ForegroundColor Yellow
    try {
        Invoke-WebRequest -Uri $alternativeUrl -OutFile $zipFile -UseBasicParsing
    } catch {
        Write-Host "ERROR: Failed to download FFmpeg. Please download manually from:" -ForegroundColor Red
        Write-Host "  https://www.gyan.dev/ffmpeg/builds/" -ForegroundColor White
        Write-Host "Download 'ffmpeg-6.0-full_build-shared.zip' and extract the bin/*.dll files to:" -ForegroundColor White
        Write-Host "  $outputDir" -ForegroundColor White
        exit 1
    }
}

Write-Host "Download completed. Extracting..." -ForegroundColor Yellow

# 解压
$extractDir = Join-Path $tempDir "ffmpeg_extracted"
if (Test-Path $extractDir) {
    Remove-Item -Path $extractDir -Recurse -Force
}

Expand-Archive -Path $zipFile -DestinationPath $extractDir -Force

# 查找 bin 目录中的 DLL
$binDir = Get-ChildItem -Path $extractDir -Directory -Recurse | Where-Object { $_.Name -eq "bin" } | Select-Object -First 1

if (-not $binDir) {
    Write-Host "ERROR: Could not find bin directory in the archive." -ForegroundColor Red
    exit 1
}

# 复制所有 DLL 到输出目录
Write-Host "Copying DLLs to output directory..." -ForegroundColor Yellow
$dllFiles = Get-ChildItem -Path $binDir.FullName -Filter "*.dll"
$copiedCount = 0

foreach ($dll in $dllFiles) {
    $destPath = Join-Path $outputDir $dll.Name
    Copy-Item -Path $dll.FullName -Destination $destPath -Force
    Write-Host "  Copied: $($dll.Name)" -ForegroundColor Gray
    $copiedCount++
}

# 清理临时文件
Write-Host "Cleaning up temporary files..." -ForegroundColor Yellow
Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "=== Done ===" -ForegroundColor Green
Write-Host "Copied $copiedCount DLL files to: $outputDir"
Write-Host ""
Write-Host "FFmpeg libraries installed successfully!" -ForegroundColor Green
Write-Host "You can now run the Dorisoy.Meeting.Client application."
