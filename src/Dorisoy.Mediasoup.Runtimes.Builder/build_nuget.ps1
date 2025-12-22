# PowerShell script to build Dorisoy.Mediasoup.Runtimes NuGet package
# Usage: .\build_nuget.ps1 [-Version "3.15.8"]

param(
    [string]$Version = "3.15.8"
)

$ErrorActionPreference = "Stop"

$PACKAGE_NAME = "Dorisoy.Mediasoup.Runtimes"
$OUT_DIR = "output"
$WORK_DIR = "build"
$BASE_URL = "https://github.com/versatica/mediasoup/releases/download/$Version"

# Platform mapping: GitHub release name -> .NET RID
$PLATFORMS = @(
    @{ Key = "darwin-arm64"; RID = "osx-arm64"; Ext = "" },
    @{ Key = "darwin-x64"; RID = "osx-x64"; Ext = "" },
    @{ Key = "linux-arm64-kernel6"; RID = "linux-arm64"; Ext = "" },
    @{ Key = "linux-x64-kernel6"; RID = "linux-x64"; Ext = "" },
    @{ Key = "win32-x64"; RID = "win-x64"; Ext = ".exe" }
)

Write-Host "➡️ Cleaning old build directory..." -ForegroundColor Cyan
if (Test-Path "$WORK_DIR/runtimes") {
    Remove-Item -Recurse -Force "$WORK_DIR/runtimes"
}
New-Item -ItemType Directory -Force -Path $WORK_DIR | Out-Null
New-Item -ItemType Directory -Force -Path $OUT_DIR | Out-Null

Write-Host "📦 Downloading and extracting files..." -ForegroundColor Cyan
foreach ($platform in $PLATFORMS) {
    $key = $platform.Key
    $rid = $platform.RID
    $url = "$BASE_URL/mediasoup-worker-$Version-$key.tgz"
    $tgzFile = "$WORK_DIR/$key-$Version.tgz"
    $nativeDir = "$WORK_DIR/runtimes/$rid/native"

    if (Test-Path $tgzFile) {
        Write-Host "✅ Already exists: $tgzFile, skipping download" -ForegroundColor Green
    } else {
        Write-Host "🔽 Downloading $url" -ForegroundColor Yellow
        try {
            Invoke-WebRequest -Uri $url -OutFile $tgzFile -UseBasicParsing
        } catch {
            Write-Host "❌ Failed to download $url : $_" -ForegroundColor Red
            continue
        }
    }

    Write-Host "📂 Extracting to runtimes/$rid/native/" -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path $nativeDir | Out-Null
    
    # Use tar (available in Windows 10+)
    tar -xzf $tgzFile -C $nativeDir
}

Write-Host "📝 Generating .nuspec file..." -ForegroundColor Cyan
$nuspecContent = @"
<?xml version="1.0"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
  <metadata>
    <id>$PACKAGE_NAME</id>
    <version>$Version</version>
    <authors>Alby</authors>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Cross-platform mediasoup-worker executables for multiple runtimes.</description>
    <tags>mediasoup native runtimes</tags>
  </metadata>
  <files>
"@

foreach ($platform in $PLATFORMS) {
    $rid = $platform.RID
    $ext = $platform.Ext
    $nativePath = "runtimes/$rid/native"
    $workerFile = "mediasoup-worker$ext"
    
    if (Test-Path "$WORK_DIR/$nativePath/$workerFile") {
        $nuspecContent += "`n    <file src=`"$nativePath/$workerFile`" target=`"$nativePath/$workerFile`" />"
    }
}

$nuspecContent += @"

  </files>
</package>
"@

$nuspecPath = "$WORK_DIR/$PACKAGE_NAME.nuspec"
$nuspecContent | Out-File -FilePath $nuspecPath -Encoding UTF8

Write-Host "📦 Building NuGet package..." -ForegroundColor Cyan
Push-Location $WORK_DIR
try {
    # Try dotnet pack first, fallback to nuget.exe
    if (Get-Command nuget -ErrorAction SilentlyContinue) {
        nuget pack "$PACKAGE_NAME.nuspec" -OutputDirectory "../$OUT_DIR"
    } else {
        Write-Host "⚠️ nuget.exe not found. Please install NuGet CLI or use:" -ForegroundColor Yellow
        Write-Host "   dotnet tool install --global NuGet.CommandLine" -ForegroundColor Yellow
        Write-Host "   Or download from: https://www.nuget.org/downloads" -ForegroundColor Yellow
        
        # Alternative: create package manually
        $nupkgPath = "../$OUT_DIR/$PACKAGE_NAME.$Version.nupkg"
        Compress-Archive -Path "runtimes", "$PACKAGE_NAME.nuspec" -DestinationPath $nupkgPath -Force
        Rename-Item $nupkgPath ($nupkgPath -replace "\.zip$", ".nupkg") -ErrorAction SilentlyContinue
        Write-Host "📦 Created package using Compress-Archive (may need manual adjustment)" -ForegroundColor Yellow
    }
} finally {
    Pop-Location
}

$outputPath = "$OUT_DIR/$PACKAGE_NAME.$Version.nupkg"
if (Test-Path $outputPath) {
    Write-Host "✅ Package created: $outputPath" -ForegroundColor Green
    Write-Host ""
    Write-Host "To use this package locally, add a local NuGet source:" -ForegroundColor Cyan
    Write-Host "  dotnet nuget add source `"$(Resolve-Path $OUT_DIR)`" --name LocalPackages" -ForegroundColor White
} else {
    Write-Host "⚠️ Package may not have been created properly. Check the output directory." -ForegroundColor Yellow
}
