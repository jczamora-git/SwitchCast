<#
.SYNOPSIS
    Builds the professional Windows installer for SwitchCast using Inno Setup.
.DESCRIPTION
    1. Validates build prerequisites (.NET 8 SDK, Inno Setup 6, SignTool if signing enabled).
    2. Publishes SwitchCast as a self-contained WinUI 3 desktop application for Windows x64.
    3. Verifies published payload integrity.
    4. Optionally signs application binaries (SwitchCast.exe) with Authenticode SHA-256.
    5. Compiles the Inno Setup script (installer/SwitchCast.iss) with LZMA2 compression.
    6. Optionally signs the uninstaller and final setup wizard executable via Inno Setup SignTool bridge.
    7. Verifies signatures with Windows SDK SignTool (`signtool verify /pa /v`).
    8. Computes SHA-256 checksums and reports build status with clear dev/prod distinction.
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$Version = "1.5.0",
    [switch]$SkipPublish,

    # Optional Production Code Signing Parameters
    [switch]$Sign,
    [string]$CertPath = $env:SWITCHCAST_CERT_PATH,
    [string]$CertPassword = $env:SWITCHCAST_CERT_PASSWORD,
    [string]$CertThumbprint = $env:SWITCHCAST_CERT_THUMBPRINT,
    [string]$CertSubject = $env:SWITCHCAST_CERT_SUBJECT,
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [string]$FallbackTimestampUrl = "http://timestamp.sectigo.com",
    [switch]$AllowUntrustedRoot = ($env:SWITCHCAST_ALLOW_UNTRUSTED_ROOT -eq "1"),
    [switch]$CheckSignatureOnly
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path
$issFile = Join-Path $PSScriptRoot "SwitchCast.iss"
$publishDir = Join-Path $repoRoot "publish"
$releasesDir = Join-Path $repoRoot "releases"
$setupExeName = "SwitchCast-Setup-v$Version-x64.exe"
$setupExePath = Join-Path $releasesDir $setupExeName
$shaPath = Join-Path $releasesDir "SwitchCast-Setup-v$Version-x64.sha256"
$appExePath = Join-Path $publishDir "SwitchCast.exe"
$signBridgeCmd = Join-Path $PSScriptRoot "sign-file.cmd"
$signTargetPs1 = Join-Path $PSScriptRoot "sign-target.ps1"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " SwitchCast v$Version - Windows Installer Build Engine" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 0. Check Signatures Only Mode
if ($CheckSignatureOnly) {
    Write-Host ""
    Write-Host "[0/5] Checking Authenticode signature status..." -ForegroundColor Yellow

    $targets = @($appExePath, $setupExePath)
    foreach ($target in $targets) {
        if (Test-Path $target) {
            Write-Host "`nTarget: $target" -ForegroundColor Cyan
            $sig = Get-AuthenticodeSignature -FilePath $target
            Write-Host "  Authenticode Status : $($sig.Status)"
            if ($sig.SignerCertificate) {
                Write-Host "  Signer Subject      : $($sig.SignerCertificate.Subject)"
                Write-Host "  Signer Issuer       : $($sig.SignerCertificate.Issuer)"
                Write-Host "  Signer Thumbprint   : $($sig.SignerCertificate.Thumbprint)"
            } else {
                Write-Host "  Signer Certificate  : NONE (Unsigned)"
            }
            if ($sig.TimeStamperCertificate) {
                Write-Host "  Timestamp Issuer    : $($sig.TimeStamperCertificate.Issuer)"
            }
        } else {
            Write-Host "`nTarget not found: $target" -ForegroundColor DarkGray
        }
    }
    exit 0
}

# 1. Validate Prerequisites
Write-Host ""
Write-Host "[1/6] Validating build prerequisites..." -ForegroundColor Yellow

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

# Validate Code Signing Prerequisites (if -Sign requested)
if ($Sign) {
    Write-Host "  Validating production code-signing credentials..." -ForegroundColor Yellow
    if (-not (Test-Path $signBridgeCmd) -or -not (Test-Path $signTargetPs1)) {
        Write-Error "CRITICAL: SignTool helper scripts missing from installer directory."
        exit 1
    }

    $hasCredentials = ($CertThumbprint -or $CertSubject -or $CertPath)
    if (-not $hasCredentials) {
        Write-Error @"
CRITICAL: Production code signing was requested with -Sign, but no signing credentials were provided.
Please provide one of:
  -CertThumbprint <SHA1> (Certificate Store)
  -CertSubject <Name>   (Certificate Store)
  -CertPath <path.pfx>  (PFX File with -CertPassword)
Or set the corresponding environment variables: SWITCHCAST_CERT_THUMBPRINT, SWITCHCAST_CERT_PATH, SWITCHCAST_CERT_PASSWORD.
"@
        exit 1
    }
    Write-Host "  Code-signing credentials confirmed." -ForegroundColor Green
} else {
    Write-Host "  Code signing not requested (building Unsigned Development package)." -ForegroundColor DarkGray
}

# 2. Publish Self-Contained WinUI 3 Application Payload
if (-not $SkipPublish) {
    Write-Host ""
    Write-Host "[2/6] Publishing self-contained WinUI 3 Release payload (win-x64)..." -ForegroundColor Yellow

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
    Write-Host "[2/6] Skipping publish step as requested." -ForegroundColor Yellow
}

# 3. Verify Publish Output Files
Write-Host ""
Write-Host "[3/6] Verifying application payload..." -ForegroundColor Yellow

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

# 4. Optional Application Binary Signing (SwitchCast.exe)
if ($Sign) {
    Write-Host ""
    Write-Host "[4/6] Signing application binary (SwitchCast.exe)..." -ForegroundColor Yellow

    $signParams = @{
        FilePath             = $appExePath
        TimestampUrl         = $TimestampUrl
        FallbackTimestampUrl = $FallbackTimestampUrl
        Description          = "SwitchCast"
        DescriptionUrl       = "https://switchcast.wasmer.app/"
        AllowUntrustedRoot   = $AllowUntrustedRoot
    }
    if ($CertThumbprint) { $signParams["CertThumbprint"] = $CertThumbprint }
    if ($CertSubject)    { $signParams["CertSubject"]    = $CertSubject }
    if ($CertPath)       { $signParams["CertPath"]       = $CertPath }
    if ($CertPassword)   { $signParams["CertPassword"]   = $CertPassword }

    & $signTargetPs1 @signParams
    if ($LASTEXITCODE -ne 0) {
        Write-Error "CRITICAL: Signing SwitchCast.exe failed with exit code $LASTEXITCODE."
        exit $LASTEXITCODE
    }
    Write-Host "  SwitchCast.exe Authenticode signature applied and verified." -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "[4/6] Skipping binary signing (Unsigned development mode)." -ForegroundColor DarkGray
}

# 5. Compile Inno Setup Script
Write-Host ""
Write-Host "[5/6] Compiling Inno Setup script with LZMA2/ultra64 compression..." -ForegroundColor Yellow

if (-not (Test-Path $releasesDir)) {
    New-Item -ItemType Directory -Path $releasesDir -Force | Out-Null
}

$isccArgs = @(
    "/Qp",
    "/O$releasesDir",
    "/FSwitchCast-Setup-v$Version-x64",
    "/DMyAppVersion=$Version",
    "/DMyPublishDir=$publishDir"
)

if ($Sign) {
    Write-Host "  Configuring Inno Setup SignTool bridge with Authenticode signing..." -ForegroundColor Cyan
    # Export credentials to current process environment so sign-file.cmd bridge inherits them securely
    if ($CertThumbprint) { $env:SWITCHCAST_CERT_THUMBPRINT = $CertThumbprint }
    if ($CertSubject)    { $env:SWITCHCAST_CERT_SUBJECT    = $CertSubject }
    if ($CertPath)       { $env:SWITCHCAST_CERT_PATH       = $CertPath }
    if ($CertPassword)   { $env:SWITCHCAST_CERT_PASSWORD   = $CertPassword }
    if ($TimestampUrl)   { $env:SWITCHCAST_TIMESTAMP_URL   = $TimestampUrl }
    $env:SWITCHCAST_ALLOW_UNTRUSTED_ROOT = if ($AllowUntrustedRoot) { "1" } else { "0" }

    $isccArgs += "/DEnableSigning"
    $isccArgs += "/Sstandard=$signBridgeCmd `$p"
}

Write-Host "  Executing ISCC compilation: $issFile" -ForegroundColor DarkGray
& "$isccPath" $isccArgs "$issFile"

# Clean up transient environment variables
if ($Sign) {
    Remove-Item Env:\SWITCHCAST_CERT_PASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:\SWITCHCAST_ALLOW_UNTRUSTED_ROOT -ErrorAction SilentlyContinue
}

if ($LASTEXITCODE -ne 0) {
    Write-Error "CRITICAL: Inno Setup compilation failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

# 6. Verify Installer Output, Signature & Compute Checksums
Write-Host ""
Write-Host "[6/6] Verifying installer output and calculating SHA-256..." -ForegroundColor Yellow

if (-not (Test-Path $setupExePath)) {
    Write-Error "CRITICAL: Expected installer output not found at: $setupExePath"
    exit 1
}

$hash = (Get-FileHash -Path $setupExePath -Algorithm SHA256).Hash
Set-Content -Path $shaPath -Value "$hash  $setupExeName" -Encoding ASCII

$installerSizeBytes = (Get-Item $setupExePath).Length
$installerSizeMB = [Math]::Round(($installerSizeBytes / 1048576.0), 2)

# Verify installer signature
$installerSig = Get-AuthenticodeSignature -FilePath $setupExePath

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host " BUILD SUCCESSFUL!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "  Installer Path : $setupExePath"
Write-Host "  File Size      : $installerSizeMB MB ($installerSizeBytes bytes)"
Write-Host "  SHA-256 Hash   : $hash"
Write-Host "  Checksum File  : $shaPath"

if ($Sign) {
    Write-Host ""
    Write-Host "----------------------------------------------------------" -ForegroundColor Cyan
    Write-Host " PRODUCTION CODE SIGNING VERIFICATION" -ForegroundColor Cyan
    Write-Host "----------------------------------------------------------" -ForegroundColor Cyan
    Write-Host "  Status         : $($installerSig.Status)"
    if ($installerSig.SignerCertificate) {
        Write-Host "  Publisher      : $($installerSig.SignerCertificate.Subject)"
        Write-Host "  Issuer         : $($installerSig.SignerCertificate.Issuer)"
        Write-Host "  Thumbprint     : $($installerSig.SignerCertificate.Thumbprint)"
    }
    if ($installerSig.TimeStamperCertificate) {
        Write-Host "  Timestamp By   : $($installerSig.TimeStamperCertificate.Subject)"
    }
    Write-Host "----------------------------------------------------------" -ForegroundColor Cyan

    if ($installerSig.Status -eq "NotSigned") {
        Write-Error "CRITICAL: Production signing was enabled but installer output is NotSigned."
        exit 1
    }

    if ($installerSig.Status -ne "Valid" -and -not ($AllowUntrustedRoot -and ($installerSig.Status -eq "UnknownError" -or $installerSig.Status -eq "NotTrusted"))) {
        Write-Error "CRITICAL: Production signing failed Authenticode trust verification (Status: $($installerSig.Status))."
        exit 1
    }
} else {
    Write-Host ""
    Write-Host "----------------------------------------------------------" -ForegroundColor Yellow
    Write-Host " DISTRIBUTION SECURITY NOTICE [UNSIGNED DEV BUILD]" -ForegroundColor Yellow
    Write-Host "----------------------------------------------------------" -ForegroundColor Yellow
    Write-Host "  Status         : Unsigned (Development Artifact)"
    Write-Host "  SmartScreen    : Windows Defender SmartScreen WILL display:"
    Write-Host "                   `"Windows protected your PC - Microsoft Defender SmartScreen`""
    Write-Host "                   `"prevented an unrecognized app from starting`""
    Write-Host "                   on client systems because the binary is unsigned"
    Write-Host "                   and has not yet built cloud telemetry reputation."
    Write-Host "  Production Fix : Build with -Sign and a trusted Authenticode certificate"
    Write-Host "                   (or cloud signing service like SignPath / Azure Trusted Signing)."
    Write-Host "----------------------------------------------------------" -ForegroundColor Yellow
}
Write-Host "==========================================================" -ForegroundColor Green
