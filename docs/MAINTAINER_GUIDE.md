# SwitchCast — GitHub Maintainer Setup & Governance Guide

This document is the authoritative operational guide for authorized repository maintainers ([@jczamora-git](https://github.com/jczamora-git)). It outlines repository governance, pull request review procedures, release workflows, and the **manual GitHub configuration settings** required on GitHub Web to enforce repository protection.

---

## 1. Core Maintainer Principles

1. **Sole Merge & Release Authority**: Only authorized maintainers have authority to merge pull requests into `main` and publish official releases, git tags, and installer binaries.
2. **Community Contributions via PRs**: All contributions from external developers must arrive via pull requests from forks. Direct commits to `main` by non-maintainers are prohibited.
3. **No Unapproved Technology Introductions**: Guard the codebase against unwanted runtimes (Electron, Tauri, Node), unnecessary cloud telemetry, unvetted NuGet dependencies, or architectural layer violations (native interop directly inside XAML Views or ViewModels).
4. **Preserve Privacy & Zero Telemetry**: Every PR must be vetted to guarantee that captured frames, presenter window titles, and diagnostic metrics remain strictly local on the client machine.

---

## 2. GitHub Settings Requiring Manual Configuration

The following repository settings cannot be set via repository files and **must be configured manually by the repository owner** in the GitHub Web interface:

### A. Branch Protection / Repository Rulesets (`main` branch)
Navigate to: **Repository Settings > Branches** (or **Settings > Rules > Rulesets**)

Configure a protection rule or ruleset targeting the default branch `main`:

| Setting | Recommended Value | Purpose |
| :--- | :--- | :--- |
| **Branch name pattern** | `main` | Targets default production branch |
| **Require a pull request before merging** | **Enabled** | Enforces that all code arrives via PRs |
| **Required approvals** | `1` | Ensures maintainer review |
| **Dismiss stale pull request approvals** | **Enabled** | Invalidates approvals when new commits are pushed |
| **Require review from Code Owners** | **Enabled** | Enforces that `.github/CODEOWNERS` (@jczamora-git) must review/approve |
| **Require status checks to pass before merging** | **Enabled** | Prevents merging broken builds or failing tests |
| **Status check: `build`** | **Selected** | Requires the GitHub Actions release/build job to pass |
| **Require branches to be up to date** | **Enabled** | Ensures PRs are tested against latest `main` |
| **Do not allow bypassing the above settings** | **Enabled** | Enforces rules even for repository admins |
| **Allow force pushes** | **Disabled** | Protects linear history against accidental overwrites |
| **Allow deletions** | **Disabled** | Prevents accidental deletion of `main` |

---

### B. Private Vulnerability Reporting
Navigate to: **Repository Settings > Code security and analysis**

- Find **"Private vulnerability reporting"**
- Click **Enable**
- **Purpose**: Enables the private security reporting link referenced in [SECURITY.md](../SECURITY.md) and [.github/ISSUE_TEMPLATE/config.yml](../.github/ISSUE_TEMPLATE/config.yml). This permits security researchers to disclose vulnerabilities privately via GitHub Security Advisories rather than public issues.

---

### C. GitHub Actions Workflow Permissions
Navigate to: **Repository Settings > Actions > General > Workflow permissions**

- Select **"Read and write permissions"**
- Check **"Allow GitHub Actions to create and approve pull requests"** (optional / as needed)
- Click **Save**
- **Purpose**: The release workflow (`.github/workflows/release.yml`) uses `softprops/action-gh-release` to upload assets (`SwitchCast-Setup-v*-x64.exe` and `SwitchCast-*-win-x64.zip`) and publish GitHub release notes. Write permissions are required to create releases and upload release assets.

---

### D. GitHub Discussions (Optional)
Navigate to: **Repository Settings > General > Features**

- Check or uncheck **"Discussions"**
- **Purpose**: Enable if you want community members to ask general questions, share presenter workflows, or discuss ideas without opening formal bug or feature tracking issues.

---

## 3. Pull Request Review Protocol

When reviewing a community Pull Request, follow this verification checklist:

### Step 1: Architectural Boundary Check
- [ ] No native Direct3D 11, COM, or Windows Graphics Capture interop inside XAML Views or ViewModels.
- [ ] No Win32 API calls (`User32`, `DwmApi`) placed directly in ViewModels.
- [ ] UI updates correctly marshaled via `DispatcherQueue`.
- [ ] Proper `IDisposable` implementation on new native resources or device wrappers.

### Step 2: Privacy & Security Check
- [ ] Zero network requests, cloud telemetry, or remote data exfiltration.
- [ ] No window titles, frame pixels, or sensitive user inputs written to disk logs.
- [ ] OS window protection (`SetWindowDisplayAffinity`) respected.

### Step 3: Verification Check
- [ ] All 480+ automated unit tests pass:
  ```powershell
  dotnet test SwitchCast.Tests/SwitchCast.Tests.csproj -c Release
  ```
- [ ] Solution compiles cleanly on both `Debug` and `Release` (0 errors, 0 new warnings).
- [ ] Windows Installer builds cleanly:
  ```powershell
  .\installer\build-installer.ps1 -SkipPublish
  ```

---

## 4. Release Publishing Protocol

To publish an official SwitchCast release:

1. **Verify Working Tree & Test Matrix**:
   Ensure all automated tests pass with 100% success rate:
   ```powershell
   dotnet test SwitchCast.Tests/SwitchCast.Tests.csproj -c Release
   ```

2. **Update Version Metadata**:
   Update version string across:
   - `SwitchCast.csproj` (`<AssemblyVersion>`, `<FileVersion>`, `<Version>`)
   - `app.manifest` (`assemblyIdentity version="..."`)
   - `ViewModels/SettingsViewModel.cs` (`AppVersion`)
   - `README.md` (Release badge and download links)
   - `docs/releases/v<Version>.md` (Authored release notes)

3. **Verify Installer Compilation**:
   ```powershell
   .\installer\build-installer.ps1
   ```

4. **Commit Locally**:
   ```powershell
   git add .
   git commit -m "chore(release): prepare vX.Y.Z release"
   ```

5. **Tag and Push**:
   ```powershell
   git tag -a vX.Y.Z -m "SwitchCast vX.Y.Z — <Title>"
   git push origin main
   git push origin vX.Y.Z
   ```

6. **Monitor GitHub Actions**:
   - The `.github/workflows/release.yml` workflow triggers automatically on tag `v*`.
   - The runner runs unit tests, publishes the self-contained WinUI 3 payload, compiles the Inno Setup installer, packages the portable zip, and publishes the official release on GitHub Releases.
