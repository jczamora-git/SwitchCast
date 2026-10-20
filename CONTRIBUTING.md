# Contributing to SwitchCast

Thank you for your interest in contributing to **SwitchCast**! We welcome community contributions that improve stability, performance, usability, and presenter privacy.

Please take a few moments to review these guidelines before submitting code or proposing changes.

---

## 1. Governance & Contribution Policy

To ensure high reliability, stability during live presentations, and security:

1. **Pull Requests Only**: All external community contributions must be submitted via **Pull Requests (PRs)** from your personal fork. Direct pushes to the `main` branch are restricted.
2. **Maintainer Authority**: Official merge authority and release publishing rights remain exclusively with authorized project maintainers ([@jczamora-git](https://github.com/jczamora-git)). Pull requests are reviewed, approved, and merged at maintainer discretion.
3. **Issue First for Non-Trivial Changes**: For significant enhancements, architectural alterations, or new features, please open an Issue to discuss your proposal before investing time into an implementation.
4. **Smallest Justified Change**: We value targeted, minimal, backward-compatible modifications. Avoid opportunistic refactoring, unsolicited stylistic reformatting, or bundling unrelated fixes into a single PR.

---

## 2. Architecture & Design Principles

SwitchCast is a high-performance Windows desktop application built with **.NET 8** and **WinUI 3 (Windows App SDK 1.5)**. All contributions must respect our architectural boundaries:

```
[ WinUI 3 Views (XAML) ]
           │
           ▼
[ ViewModels (MVVM / CommunityToolkit.Mvvm) ]
           │
           ▼
[ Services & Abstractions (ICaptureService, IPresentationCoordinator) ]
           │
           ▼
[ Native Platform Interop (WGC, Direct3D 11, Win32) ]
```

### Mandatory Architectural Rules
- **Layer Isolation**:
  - Never place native capture interop or Direct3D logic inside XAML Views or ViewModels.
  - Never place business logic or frame transitions inside View code-behind.
  - Never make Win32 API calls directly inside ViewModels; encapsulate them in injectable service abstractions.
- **Privacy First & Zero Telemetry**:
  - Captured frames and textures must remain local in system/GPU memory. Never introduce cloud endpoints, analytics tracking, or remote network transmission.
  - Respect OS display affinity (`SetWindowDisplayAffinity`).
- **Standard User Privileges**:
  - The application must execute under standard user rights without requiring administrator elevation.
- **Resource Lifecycle & Thread Safety**:
  - All native COM wrappers, swapchains, and capture sessions must implement explicit `IDisposable` lifecycle management.
  - All UI state changes must be dispatched onto the WinUI 3 UI thread via `DispatcherQueue`.

---

## 3. Development Environment Setup

### Prerequisites
- **Operating System**: Windows 10 (version 1809+ / build 17763+) or Windows 11 (x64)
- **SDK**: .NET 8.0 SDK (version 8.0.x or higher)
- **IDE**: Visual Studio 2022 (v17.8+ with *".NET Desktop Development"* and *"Windows App SDK C# Templates"*) or Visual Studio Code with the C# Dev Kit extension.
- **Installer Tooling (Optional)**: [Inno Setup 6](https://jrsoftware.org/isinfo.php) if working on installer packaging (`winget install JRSoftware.InnoSetup -e`).

### Building Locally

Clone your fork and build the solution:
```powershell
git clone https://github.com/<your-username>/SwitchCast.git
cd SwitchCast
dotnet build SwitchCast.sln -c Debug
```

### Running the Application
```powershell
dotnet run --project SwitchCast.csproj -c Debug
```

---

## 4. Quality Gates & Verification

Before submitting any Pull Request, ensure your changes satisfy all verification gates:

### 1. Automated Test Suite (Mandatory)
SwitchCast maintains an automated unit and contract test suite with 480+ tests. Run tests from the repository root:
```powershell
dotnet test SwitchCast.Tests/SwitchCast.Tests.csproj -c Release
```
> [!IMPORTANT]
> **100% Pass Rate Required**: Every test must pass (0 failures, 0 errors). If you add new functionality or fix a bug, accompany your change with corresponding unit tests in `SwitchCast.Tests/`.

### 2. Compiler Health
Both `Debug` and `Release` configurations must compile with:
- **0 Errors**
- **0 New Compiler Warnings**

### 3. Coding Standards
- Follow established C# 12 and .NET 8 idioms.
- Follow existing PascalCase for classes/methods/properties and camelCase with `_` prefix for private fields (`_captureService`).
- Keep XAML bindings type-safe using `{x:Bind}` where possible.
- Avoid introducing unjustified third-party NuGet dependencies.

---

## 5. Submitting a Pull Request

1. **Create a Topic Branch**:
   ```powershell
   git checkout -b feat/your-feature-name
   # or
   git checkout -b fix/issue-description
   ```
2. **Commit Your Work**:
   Use clear, conventional commit messages:
   - `feat(sources): add filter for active windows`
   - `fix(dock): prevent null reference on rapid close`
   - `docs(readme): clarify audio routing instructions`
3. **Sync with Main**:
   Ensure your branch is rebased on or up to date with the upstream `main` branch.
4. **Open the Pull Request**:
   - Fill out the provided **Pull Request Template** completely.
   - Link any related GitHub issues (e.g., `Fixes #42`).
   - Describe what changed, the architectural justification, and how you verified your change on Windows.
5. **Review & Revisions**:
   - A maintainer will review your submission. Please respond to feedback constructively.
   - Maintainers will handle merging upon approval.
