<#
.SYNOPSIS
    Builds the professional Windows installer for SwitchCast using Inno Setup.
.DESCRIPTION
    1. Validates build prerequisites (.NET 8 SDK, Inno Setup 6).
    2. Publishes SwitchCast as a self-contained WinUI 3 desktop application for Windows x64.
    3. Verifies published payload integrity.
    4. Compiles the Inno Setup script (installer/SwitchCast.iss).
    5. Computes SHA-256 checksums and reports build status.
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$Version = "1.5.0",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path
$issFile = Join-Path $PSScriptRoot "SwitchCast.iss"
$publishDir = Join-Path $repoRoot "publish"
$releasesDir = Join-Path $repoRoot "releases"
$setupExeName = "SwitchCast-Setup-v$Version-x64.exe"
$setupExePath = Join-Path $releasesDir $setupExeName
$shaPath = Join-Path $releasesDir "SwitchCast-Setup-v$Version-x64.sha256"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " SwitchCast v$Version - Windows Installer Build Engine" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Validate Prerequisites
Write-Host ""
Write-Host "[1/5] Validating build prerequisites..." -ForegroundColor Yellow

# Locate dotnet
$dotnetCmd = Get-Command "dotnet" -ErrorAction SilentlyContinue
if (-not $dotnetCmd) {
    Write-Error "CRITICAL: .NET SDK (dotnet) is not available in PATH. Please install .NET 8.0 SDK."
    exit 1
}
Write-Host "  Found .NET SDK: $($dotnetCmd.Source)" -ForegroundColor Green

# Locate Inno Setup Compiler (ISCC.exe)
$isccCandidates = @(
    (Get-Command "iscc.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)

$isccPath = $null
foreach ($candidate in $isccCandidates) {
    if ($candidate -and (Test-Path $candidate)) {
        $isccPath = $candidate
        break
    }
}

if (-not $isccPath) {
    Write-Error "CRITICAL: Inno Setup 6 compiler (ISCC.exe) was not found. Please install Inno Setup 6 using: winget install JRSoftware.InnoSetup -e or download from https://jrsoftware.org/isinfo.php"
    exit 1
}
Write-Host "  Found Inno Setup Compiler: $isccPath" -ForegroundColor Green

# 2. Publish Self-Contained WinUI 3 Application Payload
if (-not $SkipPublish) {
    Write-Host ""
    Write-Host "[2/5] Publishing self-contained WinUI 3 Release payload (win-x64)..." -ForegroundColor Yellow

    if (Test-Path $publishDir) {
        Remove-Item -Recurse -Force $publishDir
    }
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

    $csprojPath = Join-Path $repoRoot "SwitchCast.csproj"

    Write-Host "  Executing dotnet publish on: $csprojPath" -ForegroundColor DarkGray
    & dotnet publish "$csprojPath" -c "$Configuration" -r "win-x64" -p:Platform="$Platform" --self-contained true -o "$publishDir"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "CRITICAL: dotnet publish failed with exit code $LASTEXITCODE."
        exit $LASTEXITCODE
    }
} else {
    Write-Host ""
    Write-Host "[2/5] Skipping publish step as requested." -ForegroundColor Yellow
}

# 3. Verify Publish Output Files
Write-Host ""
Write-Host "[3/5] Verifying application payload..." -ForegroundColor Yellow

$essentialFiles = @(
    "SwitchCast.exe",
    "SwitchCast.dll",
    "resources.pri",
    "Microsoft.ui.xaml.dll",
    "coreclr.dll"
)

foreach ($file in $essentialFiles) {
    $filePath = Join-Path $publishDir $file
    if (-not (Test-Path $filePath)) {
        Write-Error "CRITICAL: Required published file missing from '$publishDir': $file"
        exit 1
    }
}

$payloadCount = (Get-ChildItem $publishDir -Recurse -File).Count
$payloadSizeBytes = (Get-ChildItem $publishDir -Recurse -File | Measure-Object -Property Length -Sum).Sum
$payloadSizeMB = [Math]::Round(($payloadSizeBytes / 1048576.0), 2)
Write-Host "  Verified $payloadCount payload files ($payloadSizeMB MB uncompressed)." -ForegroundColor Green

# 4. Compile Inno Setup Script
Write-Host ""
Write-Host "[4/5] Compiling Inno Setup script with LZMA2/ultra64 compression..." -ForegroundColor Yellow

if (-not (Test-Path $releasesDir)) {
    New-Item -ItemType Directory -Path $releasesDir -Force | Out-Null
}

Write-Host "  Executing ISCC compilation: $issFile" -ForegroundColor DarkGray
& "$isccPath" "/Qp" "/O$releasesDir" "/FSwitchCast-Setup-v$Version-x64" "/DMyAppVersion=$Version" "/DMyPublishDir=$publishDir" "$issFile"

if ($LASTEXITCODE -ne 0) {
    Write-Error "CRITICAL: Inno Setup compilation failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

# 5. Verify Installer & Compute Checksums
Write-Host ""
Write-Host "[5/5] Verifying installer output and calculating SHA-256..." -ForegroundColor Yellow

if (-not (Test-Path $setupExePath)) {
    Write-Error "CRITICAL: Expected installer output not found at: $setupExePath"
    exit 1
}

$hash = (Get-FileHash -Path $setupExePath -Algorithm SHA256).Hash
Set-Content -Path $shaPath -Value "$hash  $setupExeName" -Encoding ASCII

$installerSizeBytes = (Get-Item $setupExePath).Length
$installerSizeMB = [Math]::Round(($installerSizeBytes / 1048576.0), 2)

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host " BUILD SUCCESSFUL!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "  Installer Path : $setupExePath"
Write-Host "  File Size      : $installerSizeMB MB ($installerSizeBytes bytes)"
Write-Host "  SHA-256 Hash   : $hash"
Write-Host "  Checksum File  : $shaPath"
Write-Host "==========================================================" -ForegroundColor Green
