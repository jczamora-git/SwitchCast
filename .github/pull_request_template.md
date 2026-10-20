## Description

Please provide a concise description of the changes proposed in this Pull Request. Explain the rationale, bug fix, or feature enhancement, and describe any non-obvious design decisions.

Fixes #(issue number)

---

## Architectural Layer Impact

Please check all layers affected by this change:
- [ ] **Views (XAML)**: User interface, layout, styling, WinUI controls
- [ ] **ViewModels**: Presentation logic, observable bindings, command handlers
- [ ] **Services / Application**: Core business logic, coordinators, state pipelines
- [ ] **Infrastructure / Capture**: Native Win32, Windows.Graphics.Capture, Direct3D 11
- [ ] **Installer / Packaging**: Inno Setup script, build pipelines, release automation
- [ ] **Tests / Documentation**: Unit tests, architectural docs, guidelines

---

## Verification & Quality Checklist

Before requesting maintainer review, please verify that your changes satisfy the following requirements:

- [ ] **Automated Test Suite**: I ran `dotnet test SwitchCast.Tests/SwitchCast.Tests.csproj -c Release` and verified that **100% of tests pass** (0 failures, 0 errors).
- [ ] **New Tests Added**: I added comprehensive unit tests in `SwitchCast.Tests/` covering any new functionality or regression cases.
- [ ] **Compiler Health**: Clean build on both `Debug` and `Release` configurations with **0 errors and 0 new warnings**.
- [ ] **Clean Architecture Compliance**:
  - Native interop and Direct3D logic are isolated behind injectable service abstractions (never in Views or ViewModels).
  - UI state updates are properly dispatched to the WinUI thread via `DispatcherQueue`.
  - Native resources (Direct3D swapchains, COM objects, capture sessions) implement deterministic `IDisposable` lifecycle management.
- [ ] **Privacy & Security**:
  - The change adheres strictly to the 100% offline, zero-telemetry policy.
  - No network egress, cloud dependencies, or tracking were introduced.
  - OS window protection (`SetWindowDisplayAffinity`) is respected where applicable.
- [ ] **Scope Control**: This PR makes the smallest justified change and does not bundle unrelated refactoring or stylistic reformatting.
- [ ] **Documentation**: Updated relevant documentation in `docs/` or `README.md` if public contracts or workflows were modified.
