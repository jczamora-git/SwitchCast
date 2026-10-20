<#
.SYNOPSIS
    Signs and verifies Windows executables and installers using SignTool (Authenticode).
.DESCRIPTION
    Supports:
    - Certificate Store (CurrentUser / LocalMachine) by SHA-1 Thumbprint or Subject Name
    - PFX / PKCS#12 file with password (read securely from parameter or environment variable)
    - SHA-256 digest algorithm (/fd SHA256)
    - RFC 3161 trusted timestamping (/tr <url> /td SHA256) with automatic fallback
    - Signature verification using `signtool verify /pa /v` and `Get-AuthenticodeSignature`
    - Safe credential handling (passwords and private keys are never logged)
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$FilePath,

    [string]$CertPath = $env:SWITCHCAST_CERT_PATH,
    [string]$CertPassword = $env:SWITCHCAST_CERT_PASSWORD,
    [string]$CertThumbprint = $env:SWITCHCAST_CERT_THUMBPRINT,
    [string]$CertSubject = $env:SWITCHCAST_CERT_SUBJECT,
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [string]$FallbackTimestampUrl = "http://timestamp.sectigo.com",
    [string]$Description = "SwitchCast",
    [string]$DescriptionUrl = "https://switchcast.wasmer.app/",
    [switch]$AllowUntrustedRoot = ($env:SWITCHCAST_ALLOW_UNTRUSTED_ROOT -eq "1"),
    [switch]$VerifyOnly
)

$ErrorActionPreference = "Stop"

# 1. Validate Target File
if (-not (Test-Path $FilePath)) {
    Write-Error "Target file to sign not found: $FilePath"
    exit 1
}

$resolvedPath = (Resolve-Path $FilePath).Path

# 2. Locate SignTool.exe from Windows SDK
function Find-SignTool {
    $cmd = Get-Command "signtool.exe" -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    $sdkKits = "C:\Program Files (x86)\Windows Kits\10\bin"
    if (Test-Path $sdkKits) {
        $sdkTools = Get-ChildItem -Path $sdkKits -Recurse -Filter "signtool.exe" -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -like "*\x64\signtool.exe" } |
            Sort-Object -Property FullName -Descending

        if ($sdkTools -and $sdkTools.Count -gt 0) {
            return $sdkTools[0].FullName
        }
    }

    $ack = "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\signtool.exe"
    if (Test-Path $ack) {
        return $ack
    }

    return $null
}

$signtoolPath = Find-SignTool
if (-not $signtoolPath) {
    Write-Error "CRITICAL: Windows SDK SignTool (signtool.exe) was not found. Please install the Windows 10/11 SDK."
    exit 1
}

# 3. Verify-Only Flow
if ($VerifyOnly) {
    Write-Host "Verifying Authenticode signature for: $resolvedPath" -ForegroundColor Cyan
    $verifyOutput = & "$signtoolPath" verify /pa /v "$resolvedPath" 2>&1
    $exitCode = $LASTEXITCODE

    $authSig = Get-AuthenticodeSignature -FilePath "$resolvedPath"
    Write-Host "  Authenticode Status: $($authSig.Status)"
    if ($authSig.SignerCertificate) {
        Write-Host "  Signer Subject     : $($authSig.SignerCertificate.Subject)"
        Write-Host "  Signer Issuer      : $($authSig.SignerCertificate.Issuer)"
        Write-Host "  Signer Thumbprint  : $($authSig.SignerCertificate.Thumbprint)"
    }
    if ($authSig.TimeStamperCertificate) {
        Write-Host "  Timestamp Issuer   : $($authSig.TimeStamperCertificate.Issuer)"
    }

    if ($exitCode -ne 0 -or $authSig.Status -ne "Valid") {
        Write-Error "Signature verification failed (Exit Code: $exitCode, Status: $($authSig.Status))."
        exit 1
    }

    Write-Host "  Signature is VALID." -ForegroundColor Green
    exit 0
}

# 4. Resolve Signing Credentials
$signArgs = @("sign", "/fd", "SHA256")

if ($Description) {
    $signArgs += @("/d", $Description)
}
if ($DescriptionUrl) {
    $signArgs += @("/du", $DescriptionUrl)
}

$usingCert = $false

if ($CertThumbprint) {
    $cleanThumb = ($CertThumbprint -replace '\s', '').ToUpperInvariant()
    $signArgs += @("/sha1", $cleanThumb)
    Write-Host "  Using certificate from Certificate Store (Thumbprint: $cleanThumb)..." -ForegroundColor Cyan
    $usingCert = $true
} elseif ($CertSubject) {
    $signArgs += @("/n", $CertSubject)
    Write-Host "  Using certificate from Certificate Store (Subject: $CertSubject)..." -ForegroundColor Cyan
    $usingCert = $true
} elseif ($CertPath) {
    if (-not (Test-Path $CertPath)) {
        Write-Error "CRITICAL: Certificate file not found at: $CertPath"
        exit 1
    }
    $resolvedCert = (Resolve-Path $CertPath).Path
    $signArgs += @("/f", $resolvedCert)
    if ($CertPassword) {
        $signArgs += @("/p", $CertPassword)
    }
    Write-Host "  Using certificate file: $resolvedCert" -ForegroundColor Cyan
    $usingCert = $true
} else {
    Write-Error "CRITICAL: No signing credentials provided. Specify -CertThumbprint, -CertSubject, or -CertPath (or set environment variables)."
    exit 1
}

# 5. Execute SignTool with RFC 3161 Timestamping and Fallback Retry
function Invoke-SigningAttempt {
    param([string]$TsaUrl)

    $currentArgs = $signArgs + @("/tr", $TsaUrl, "/td", "SHA256", "$resolvedPath")
    
    # Execute without echoing password
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $signtoolPath
    $psi.Arguments = [string]::Join(" ", ($currentArgs | ForEach-Object {
        if ($_ -match '\s' -or $_ -eq "") {
            "`"$_`""
        } else {
            $_
        }
    }))
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    $proc = [System.Diagnostics.Process]::Start($psi)
    $stdout = $proc.StandardOutput.ReadToEnd()
    $stderr = $proc.StandardError.ReadToEnd()
    $proc.WaitForExit()

    return @{
        ExitCode = $proc.ExitCode
        Output   = ($stdout + "`n" + $stderr).Trim()
    }
}

Write-Host "  Signing: $resolvedPath" -ForegroundColor Yellow
$maxRetries = 3
$signSuccess = $false
$lastResult = $null

foreach ($tsa in @($TimestampUrl, $FallbackTimestampUrl)) {
    if (-not $tsa) { continue }
    for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
        Write-Host "  Attempt $attempt/$maxRetries using timestamp server: $tsa..." -ForegroundColor DarkGray
        $result = Invoke-SigningAttempt -TsaUrl $tsa
        $lastResult = $result
        if ($result.ExitCode -eq 0) {
            $signSuccess = $true
            break
        }
        Write-Warning "  Signing attempt $attempt failed (ExitCode: $($result.ExitCode)). Retrying in 2 seconds..."
        Start-Sleep -Seconds 2
    }
    if ($signSuccess) { break }
}

if (-not $signSuccess) {
    # Sanitize output so passwords never leak to logs
    $sanitized = $lastResult.Output
    if ($CertPassword) {
        $sanitized = $sanitized.Replace($CertPassword, "********")
    }
    Write-Error "CRITICAL: SignTool failed with exit code $($lastResult.ExitCode):`n$sanitized"
    exit $lastResult.ExitCode
}

Write-Host "  Successfully signed binary." -ForegroundColor Green

# 6. Verify Signature
Write-Host "  Verifying Authenticode signature with signtool verify /pa /v..." -ForegroundColor Yellow
$verifyPsi = New-Object System.Diagnostics.ProcessStartInfo
$verifyPsi.FileName = $signtoolPath
$verifyPsi.Arguments = "verify /pa /v `"$resolvedPath`""
$verifyPsi.UseShellExecute = $false
$verifyPsi.RedirectStandardOutput = $true
$verifyPsi.RedirectStandardError = $true
$verifyPsi.CreateNoWindow = $true

$vProc = [System.Diagnostics.Process]::Start($verifyPsi)
$vOut = $vProc.StandardOutput.ReadToEnd()
$vErr = $vProc.StandardError.ReadToEnd()
$vProc.WaitForExit()

$auth = Get-AuthenticodeSignature -FilePath "$resolvedPath"
Write-Host "  Authenticode Status: $($auth.Status)" -ForegroundColor Green
if ($auth.SignerCertificate) {
    Write-Host "  Signer Subject     : $($auth.SignerCertificate.Subject)" -ForegroundColor Green
    Write-Host "  Signer Issuer      : $($auth.SignerCertificate.Issuer)" -ForegroundColor Green
    Write-Host "  Signer Thumbprint  : $($auth.SignerCertificate.Thumbprint)" -ForegroundColor Green
}
if ($auth.TimeStamperCertificate) {
    Write-Host "  Timestamp Signer   : $($auth.TimeStamperCertificate.Subject)" -ForegroundColor Green
}

# Verification Policy Gate
if ($vProc.ExitCode -ne 0) {
    if ($auth.Status -eq "Valid") {
        Write-Host "  Signature verified successfully against trusted Windows root." -ForegroundColor Green
    } elseif ($AllowUntrustedRoot -and ($auth.Status -eq "UnknownError" -or $auth.Status -eq "NotTrusted")) {
        Write-Warning "  Self-signed or untrusted root detected (allowed via -AllowUntrustedRoot)."
        Write-Host "  Signature applied and verified for test environment." -ForegroundColor Green
    } else {
        Write-Error "CRITICAL: Signature verification failed with Windows Authenticode policy (Exit code: $($vProc.ExitCode), Status: $($auth.Status))."
        exit 1
    }
} else {
    Write-Host "  Signature strictly verified by Windows Authenticode policy." -ForegroundColor Green
}

exit 0
