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
