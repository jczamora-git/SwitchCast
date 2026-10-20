# SwitchCast — Windows Installation Wizard Guide

This document describes the design, build process, dependency strategy, upgrade lifecycle, and maintenance of the official Windows Setup Wizard for **SwitchCast**.

---

## 1. Overview & Strategy

The SwitchCast installer provides a simple, modern, and reliable setup experience for presenters and desktop users. Rather than requiring users to manually extract zip archives, the installer sets up the application cleanly in the standard Windows application hierarchy with Start Menu integration, Add/Remove Programs registration, in-place upgrades, and clean uninstallation.

### Primary Characteristics
- **Format**: Single-file setup executable (`SwitchCast-Setup-v<Version>-x64.exe`).
- **Engine**: Inno Setup 6 (Modern Wizard Style, LZMA2/ultra64 solid compression).
- **Architecture**: Windows x64.
- **Default Installation Scope**: Per-user (`%LOCALAPPDATA%\Programs\SwitchCast`), requiring zero administrative / UAC elevation for standard users, with optional elevation dialog for machine-wide installations.
- **Target OS**: Windows 10 (version 1809 / build 17763 or later) & Windows 11.

---

## 2. Dependency & Deployment Strategy

### Self-Contained Deployment (Chosen Approach)
SwitchCast is published using .NET 8 self-contained deployment (`--self-contained true`) with Windows App SDK 1.5 unpackaged runtime binaries (`<WindowsPackageType>None</WindowsPackageType>`, `<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>`).

| Factor | Self-Contained (SwitchCast) | Framework-Dependent |
| :--- | :--- | :--- |
| **User Prerequisites** | **Zero**. Works out of the box on clean Windows. | Requires user to pre-install .NET 8 Desktop Runtime. |
| **Windows App SDK** | Packaged directly alongside executable. | Requires separate MSIX runtime installer or bootstrapper. |
| **Failure Rate** | Near-zero (no mismatched runtime versions). | High on enterprise / restricted presenter laptops. |
| **Payload Size** | ~166 MB uncompressed, **~60-65 MB compressed installer**. | ~25 MB uncompressed, ~15 MB compressed installer. |
| **Offline Support** | 100% offline, zero network downloads during setup. | Requires Internet connectivity to download runtimes. |

**Rationale**: For a presentation companion used in corporate and conference environments, requiring presenters to diagnose missing .NET runtimes or install Windows App SDK dependencies is unacceptable. The self-contained distribution guarantees deterministic execution on all compatible Windows 10/11 machines.

---

## 3. Persistent Identity & Upgrade Lifecycle

### Fixed Application Identity (`AppId`)
The installer uses a permanent, fixed Inno Setup `AppId`:
```ini
AppId={{E5D91C08-36D4-4B7E-9721-7B32F7FEA780}
```
> [!IMPORTANT]
> This `AppId` MUST remain identical across all future versions (v1.5.0, v1.6.0, v2.0.0, etc.). It establishes the authoritative Windows installation identity.

### Upgrade Behavior
When running a setup wizard for a newer version:
1. Inno Setup queries the Windows registry (`HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\{AppId}_is1`).
2. Detects the existing installation directory automatically.
3. Overwrites application binaries in-place with new versions.
4. Updates the version displayed in **Windows Settings > Installed Apps**.
5. Prevents duplicate uninstall entries.

### Migration from Portable ZIP Distributions (v1.0.0 – v1.4.0)
Earlier releases were distributed as portable `.zip` packages extracted manually by users.
- Running `SwitchCast-Setup-v1.5.0-x64.exe` installs the app into `%LOCALAPPDATA%\Programs\SwitchCast`.
- Existing user data stored in `%LOCALAPPDATA%\SwitchCast` (`settings.json`, `music_playlists.json`, logs) is automatically recognized and preserved.
- Users can safely delete their old manually-extracted folder.

---

## 4. Preservation of User Data

SwitchCast strictly adheres to the principle that **user data belongs to the user**:

| Data Type | Storage Location | Installer Action | Uninstaller Action |
| :--- | :--- | :--- | :--- |
| **Application Binaries** | `{app}` (`%LOCALAPPDATA%\Programs\SwitchCast`) | Replaced / Updated | **Removed** |
| **User Settings** | `%LOCALAPPDATA%\SwitchCast\settings.json` | **Preserved** | **Preserved** |
| **Music Playlists** | `%LOCALAPPDATA%\SwitchCast\music_playlists.json` | **Preserved** | **Preserved** |
| **Diagnostic Logs** | `%LOCALAPPDATA%\SwitchCast\Logs\` | **Preserved** | **Preserved** |
| **Imported Audio/Media** | User's Music / Documents / Videos | **Untouched** | **Untouched** |

Updating or uninstalling SwitchCast will **never** delete user playlists, saved settings, or original media files.

---

## 5. Running Application Safety

To prevent locked file errors (`ERROR_SHARING_VIOLATION`), the installer includes a WMI process detector:
1. Before file extraction begins, `InitializeSetup()` checks if `SwitchCast.exe` is currently running.
2. If running, a confirmation dialog prompts the user:
   > *"SwitchCast is currently running. Please close SwitchCast (including any active presentation, SwitchDeck dock, or music playback) before continuing with the installation. Click Retry once closed, or Cancel to exit setup."*
3. The installer **never force-kills** SwitchCast to prevent interrupting live presentations or corrupting ongoing state.
4. The same protection runs during uninstallation (`InitializeUninstall()`).

---

## 6. How to Build the Installer

### Prerequisites
- **Windows 10 / 11 (x64)**
- **.NET 8.0 SDK** (verify with `dotnet --version`)
- **Inno Setup 6** (install via `winget install JRSoftware.InnoSetup -e`)

### Build Command
Run the build script from PowerShell:
```powershell
.\installer\build-installer.ps1
```

The script will:
1. Validate .NET 8 SDK and Inno Setup compiler (`ISCC.exe`).
2. Run `dotnet publish` with Release x64 self-contained configuration.
3. Verify required binaries (`SwitchCast.exe`, `resources.pri`, `Microsoft.ui.xaml.dll`, `coreclr.dll`).
4. Compile `installer/SwitchCast.iss` with LZMA2/ultra64 compression.
5. Generate `releases/SwitchCast-Setup-v1.5.0-x64.exe`.
6. Compute and output the SHA-256 checksum to `releases/SwitchCast-Setup-v1.5.0-x64.sha256`.

---

## 7. Uninstallation

Users can uninstall SwitchCast via standard Windows mechanisms:
- **Windows 11**: Settings > Apps > Installed apps > SwitchCast > Uninstall
- **Windows 10**: Settings > Apps > Apps & features > SwitchCast > Uninstall

The uninstaller removes:
- All installed program binaries in `{app}`
- Start Menu and Desktop shortcuts
- Registry uninstall registrations

Personal settings, playlists, and diagnostic history remain safely intact in `%LOCALAPPDATA%\SwitchCast`.

---

## 8. Distribution Security & SmartScreen Reputation

### The Windows Defender SmartScreen Warning
When users download and launch `SwitchCast-Setup-v1.5.0-x64.exe` from the web, Windows Defender SmartScreen queries Microsoft's cloud reputation service based on the file's SHA-256 hash and digital signature.

```
Windows protected your PC
Microsoft Defender SmartScreen prevented an unrecognized app from starting.
Running this app might put your PC at risk.
```

#### Why Does This Warning Appear?
1. **Unsigned Binaries (`NotSigned`)**: Development and unsigned release builds have no cryptographic Authenticode signature. SmartScreen evaluates only the raw SHA-256 binary hash. Because every release has a new hash with zero prior download history, SmartScreen flags it as "unrecognized".
2. **Mark of the Web (MOTW)**: Files downloaded via browsers carry an NTFS `Zone.Identifier` alternate data stream (Zone 3 = Internet), which triggers the SmartScreen security check upon execution.

> [!WARNING]
> **Critical Reality: Code Signing Does NOT Grant Instant SmartScreen Reputation.**
> A common misconception is that purchasing an Authenticode certificate immediately removes the SmartScreen warning.
> - **Reputation is Earned, Not Bought**: SmartScreen reputation is an algorithmically calculated score maintained in Microsoft cloud telemetry based on download volume, runtime installation success rate, absence of malware reports, and publisher history.
> - **The Value of Code Signing**: An Authenticode certificate attaches reputation to the **certificate identity (Subject + Public Key)** rather than individual binary hashes. Once a certificate establishes reputation across its initial downloads, all subsequent releases (v1.5.1, v1.6.0, v2.0.0) signed by that same certificate inherit the established reputation immediately without repeating the warning period.
> - In June 2023, the CA/Browser Forum deprecated software-only `.pfx` issuance for publicly trusted OV/EV certificates, requiring all keys to be hosted on hardware security modules (HSM) or certified cloud signing services.

---

## 9. Production Code Signing Architecture

SwitchCast includes a dedicated, production-grade Authenticode signing pipeline using Windows SDK `SignTool` (`signtool.exe`), SHA-256 digests, and RFC 3161 trusted timestamping.

### Complete Delivery Chain Signing
When code signing is enabled, the pipeline signs every tier of the distribution:
1. **Application Payload**: `publish\SwitchCast.exe` is signed before packaging.
2. **Uninstaller**: Inno Setup generates and signs `unins000.exe` before embedding it in the installer.
3. **Setup Wizard**: The final `SwitchCast-Setup-v<Version>-x64.exe` is signed and timestamped.
4. **Verification Gate**: Every binary is strictly verified using `signtool verify /pa /v` against the Windows Authenticode verification policy.

### Safe Credential Management
- Private keys, PFX passwords, and API tokens are **strictly excluded** from source control.
- Passwords passed via `-CertPassword` or environment variable `$env:SWITCHCAST_CERT_PASSWORD` are never echoed to console logs or stored on disk.
- Inno Setup communicates with the signing engine via a transient environment bridge (`installer/sign-file.cmd` -> `installer/sign-target.ps1`).

### How to Build a Production-Signed Release

#### Option A: Windows Certificate Store (Hardware Token / YubiKey / Installed Cert)
```powershell
.\installer\build-installer.ps1 -Sign -CertThumbprint "YOUR_CERTIFICATE_SHA1_THUMBPRINT"
```

#### Option B: PKCS#12 / PFX File
```powershell
$env:SWITCHCAST_CERT_PASSWORD = "YourSecurePassword"
.\installer\build-installer.ps1 -Sign -CertPath "C:\path\to\certificate.pfx"
```

#### Option C: Check Current Signature Status
```powershell
.\installer\build-installer.ps1 -CheckSignatureOnly
```

#### Option D: Default Unsigned Development Build
```powershell
# Preserves fast, zero-dependency local builds for developers
.\installer\build-installer.ps1
```

---

## 10. Code Signing Options for SwitchCast

| Option | Cost | HSM / Token Required | SmartScreen Behavior | Ideal For |
| :--- | :--- | :--- | :--- | :--- |
| **SignPath Foundation** | **Free** (for open source) | Cloud HSM (Managed by SignPath) | Builds reputation on certificate identity across versions | **Recommended for open-source GitHub releases** |
| **Azure Trusted Signing** | Low (~$10/month + ~$9/cert) | Cloud HSM (Microsoft Azure) | Builds reputation; integrated directly with Microsoft identity | Professional cloud CI/CD pipelines |
| **Commercial OV Certificate** (DigiCert, Sectigo, etc.) | High ($300–$600/year) | Physical FIPS 140-2 Level 2 USB token or Cloud HSM | Builds reputation over initial download period | Traditional enterprise desktop distribution |
| **Microsoft Store (MSIX)** | Free ($19 one-time dev registration) | None (Microsoft signs package) | **Instant Trust** (Zero SmartScreen warning) | Broad consumer and enterprise discovery |

### Recommended Roadmap for SwitchCast:
1. **Short Term (Current)**: Keep the codebase 100% signing-ready with `build-installer.ps1 -Sign`. For open-source GitHub distribution, apply to the [SignPath Foundation](https://about.signpath.io/programs/open-source) to receive free, trusted cloud code signing integrated directly into GitHub Actions.
2. **Medium Term**: Package SwitchCast as an MSIX desktop application and submit to the Microsoft Store to gain instant trust and seamless automatic background updates.

