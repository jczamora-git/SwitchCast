# SWITCHCAST — AGENT HANDOFF RECORD

---

## Task Details
- **Task**: SWITCHCAST — Professional Branded Windows Installer Wizard & v1.5.0 Re-Release
- **Date**: 2026-10-20T19:25:00+08:00 (UTC+8)
- **Status**: Completed (Installer built, tested, and verified: SwitchCast-Setup-v1.5.0-x64.exe; 480 automated tests passing; 0 build warnings/errors)

---

## 1. Objectives & Outcomes

1. **Windows Installer Wizard (Inno Setup 6)**:
   - Configured `installer/SwitchCast.iss` with stable GUID `AppId={{E5D91C08-36D4-4B7E-9721-7B32F7FEA780}}`.
   - Per-user installation by default without UAC requirement, supporting optional elevation dialog.
   - WMI process detection (`IsAppRunning`) guarding against installing or uninstalling while SwitchCast is running.
   - Guaranteed user settings, playlists, and logs preservation.
   - Created automated `installer/build-installer.ps1` script generating `releases/SwitchCast-Setup-v1.5.0-x64.exe` (42.37 MB) and SHA-256 checksums.

2. **Main Navigation Sidebar 'Buy me a coffee' Link**:
   - Added minimal, elegant donation link in `MainWindow.xaml` above Settings.
   - Configured with `SelectsOnInvoked="False"`, Segoe MDL2 Cafe glyph `&#xEC32;`, coral accent, and PayPal URL.
   - Opens user's default browser without interrupting presentations or SwitchDeck.
   - Added 4 unit tests in `MainViewModelTests.cs`.

3. **v1.5.0 GitHub Re-Release Automation**:
   - Updated `.github/workflows/release.yml` with Inno Setup installation and installer build step.
   - Configured upload of both `SwitchCast-Setup-*-x64.exe` and `SwitchCast-*-win-x64.zip` plus SHA-256 checksums.
   - Updated release notes in `docs/releases/v1.5.0.md`.

---

## 2. Verification Evidence

1. **Automated Unit & Contract Test Suite**:
   - Total Tests: **480 Passed, 0 Failed, 0 Skipped (100% Pass Rate)**.
   - Added unit test suite `MainViewModelTests.cs` (4 tests).

2. **Compiler Quality Gate**:
   - `Debug x64`: **0 Warnings, 0 Errors**
   - `Release x64`: **0 Warnings, 0 Errors**

3. **Installer Verification**:
   - Payload: 54 files (178.69 MB uncompressed) compressed to 42.37 MB.
   - Output: `releases/SwitchCast-Setup-v1.5.0-x64.exe`
   - Checksum: `1F495B269D3E9348D1082AC147C509F6028669349E270726CE5550D68A29B576`

---

## 3. Important Files Modified

- [installer/SwitchCast.iss](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/installer/SwitchCast.iss)
- [installer/build-installer.ps1](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/installer/build-installer.ps1)
- [installer/README.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/installer/README.md)
- [MainWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/MainWindow.xaml)
- [MainWindow.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/MainWindow.xaml.cs)
- [ViewModels/MainViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/MainViewModel.cs)
- [SwitchCast.Tests/ViewModels/MainViewModelTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/ViewModels/MainViewModelTests.cs)
- [docs/releases/v1.5.0.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/releases/v1.5.0.md)
- [.github/workflows/release.yml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.github/workflows/release.yml)

