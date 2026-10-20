# SWITCHCAST — AGENT HANDOFF RECORD

---

## Task Details
- **Task**: SWITCHCAST — Installer SmartScreen & Code Signing Readiness
- **Date**: 2026-10-20T21:00:00+08:00 (UTC+8)
- **Status**: Completed (Authenticode signing toolchain implemented, verified end-to-end, dev/prod workflows distinguished; 480 automated tests passing; 0 build warnings/errors)

---

## 1. Objectives & Outcomes

1. **Authenticode Signature Investigation**:
   - Inspected `publish/SwitchCast.exe` and `releases/SwitchCast-Setup-v1.5.0-x64.exe`.
   - Verified that prior binaries were `NotSigned`, causing Windows Defender SmartScreen to evaluate only the raw binary SHA-256 hash and display the "Windows protected your PC — Microsoft Defender SmartScreen prevented an unrecognized app from starting" dialog.

2. **Dedicated SignTool Signing Engine (`installer/sign-target.ps1`, `installer/sign-file.cmd`)**:
   - Implemented automated Windows SDK SignTool discovery across Windows 10/11 SDK directories.
   - Built support for SHA-256 hashing (`/fd SHA256`) and RFC 3161 trusted timestamping (`/tr http://timestamp.digicert.com /td SHA256`) with automatic fallback to Sectigo (`http://timestamp.sectigo.com`).
   - Supports both Windows Certificate Store (CurrentUser / LocalMachine by SHA-1 Thumbprint or Subject) and PFX files with password.
   - Implemented safe credential handling: passwords and private keys are never logged to console or stored on disk; passwords are masked in error reports.

3. **Inno Setup Full Delivery Chain Integration (`installer/SwitchCast.iss`, `installer/build-installer.ps1`)**:
   - Added conditional `SignTool=standard` and `SignedUninstaller=yes` under `#ifdef EnableSigning`.
   - Signs application binaries (`SwitchCast.exe`), the uninstaller (`unins000.exe`), and the installer wizard (`SwitchCast-Setup-v1.5.0-x64.exe`).
   - Integrated `-CheckSignatureOnly` mode to query Authenticode signatures without rebuilding.
   - Preserved fast, zero-dependency unsigned local development workflow (`.\installer\build-installer.ps1`), with clear labeling of unsigned development artifacts.
   - Implemented strict verification gates using `signtool verify /pa /v` and `Get-AuthenticodeSignature`, failing production builds (`-Sign`) if signing or verification fails.

4. **Documentation & SmartScreen Reputation Guidance**:
   - Detailed in `installer/README.md` and `docs/releases/v1.5.0.md` why code signing does NOT grant instant SmartScreen reputation (reputation is algorithmically calculated on telemetry history and download count; signing binds reputation to the certificate identity across version updates).
   - Documented viable code-signing options: SignPath Foundation (free for open source), Azure Trusted Signing, Commercial OV with HSM token, and Microsoft Store (MSIX).

---

## 2. Verification Evidence

1. **Automated Unit & Contract Test Suite**:
   - Total Tests: **480 Passed, 0 Failed, 0 Skipped (100% Pass Rate)**.
   - Command: `dotnet test SwitchCast.Tests/SwitchCast.Tests.csproj -c Release`

2. **Compiler & Build Quality Gate**:
   - `Debug x64`: **0 Warnings, 0 Errors**
   - `Release x64`: **0 Warnings, 0 Errors**

3. **Production Signing Toolchain Verification**:
   - Verified `-Sign` failure gate with missing credentials (aborts with descriptive guidance).
   - Verified end-to-end signing with DigiCert RFC 3161 timestamping (`CN=DigiCert SHA256 RSA4096 Timestamp Responder 2026 1`) and `signtool verify /pa /v`.
   - Verified `-CheckSignatureOnly` reporting.

4. **Clean Unsigned Development Artifacts Restored**:
   - Output: `releases/SwitchCast-Setup-v1.5.0-x64.exe` (42.37 MB)
   - SHA-256: `FB80496874BF178E699A1C6384A91D793697B4143D32246329377555DC2623E4`
   - Checksum File: `releases/SwitchCast-Setup-v1.5.0-x64.sha256`

---

## 3. Important Files Modified & Created

- [installer/sign-target.ps1](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/installer/sign-target.ps1) (Created)
- [installer/sign-file.cmd](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/installer/sign-file.cmd) (Created)
- [installer/SwitchCast.iss](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/installer/SwitchCast.iss)
- [installer/build-installer.ps1](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/installer/build-installer.ps1)
- [installer/README.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/installer/README.md)
- [docs/releases/v1.5.0.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/releases/v1.5.0.md)
- [.agents/PROJECT_STATE.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/PROJECT_STATE.md)
- [.agents/HANDOFF.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/HANDOFF.md)
- [.agents/CHANGELOG.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/CHANGELOG.md)
