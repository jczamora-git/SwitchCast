# SWITCHCAST — AGENT HANDOFF RECORD

---

## Task Details
- **Task**: Dynamic Application Icons Runtime Fix
- **Date**: 2026-10-09T04:00:00+08:00 (UTC+8)
- **Status**: Completed

---

## 1. Objective
Fix SwitchCast's Sources page so authentic Windows application icons (Google Chrome, Visual Studio, File Explorer, Antigravity IDE, etc.) are extracted and rendered in the source list rows instead of the generic orange fallback monitor icon.

---

## 2. Root Cause Analysis & Architecture Fixes

1. **WinUI 3 Thread Affinity (`RPC_E_WRONG_THREAD`)**:
   - **Root Cause**: `Win32WindowIconService.GetIconForSourceAsync` extracted pixel data on a background thread via `await Task.Run(...).ConfigureAwait(false)` and then constructed `new SoftwareBitmapSource()` and invoked `await sourceImage.SetBitmapAsync(...)` on the ThreadPool worker thread. In WinUI 3, `SoftwareBitmapSource` is a `DependencyObject` requiring creation and manipulation on the UI thread (`DispatcherQueue`). The call threw a COM thread-affinity exception that was caught and silently returned `null`, leaving all UI rows permanently on the fallback `<FontIcon>` glyph.
   - **Fix**: Captured `DispatcherQueue` and marshalled `SoftwareBitmap` copy and `SoftwareBitmapSource.SetBitmapAsync` onto the UI thread via `_dispatcherQueue.TryEnqueue(...)`.

2. **Process Path Extraction Access Denied**:
   - **Root Cause**: `Win32WindowDiscoveryService` attempted `Process.GetProcessById(pid).MainModule?.FileName`, which throws `Win32Exception` (Access Denied) for 32/64-bit cross-architecture processes or non-elevated callers, leaving `ProcessPath = null` for many running applications.
   - **Fix**: Added native Win32 `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid)` and `QueryFullProcessImageNameW`, allowing reliable executable path queries across all user-space processes.

3. **Multi-Tier Native Win32 Icon Resolution**:
   - Extraction sequence in [Services/Win32WindowIconService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32WindowIconService.cs):
     1. Window-specific icon via `SendMessageTimeout` (`WM_GETICON` with `ICON_SMALL2` -> `ICON_SMALL` -> `ICON_BIG`) with 100ms timeout.
     2. Window class icon via `GetClassLongPtr` (`GCLP_HICONSM` -> `GCLP_HICON`).
     3. Shell/Executable icon extraction via `ExtractIconExW` (32x32) -> `SHGetFileInfoW` (`SHGFI_LARGEICON` / `SHGFI_SMALLICON`).
   - Resource lifecycle: `DestroyIcon` is strictly called on owned shell/executable handles, never on borrowed window/class handles.

4. **Accurate Alpha Transparency & DIB Rendering**:
   - Inspected icon bit depth via `GetIconInfo` (`hbmColor` bit count). Modern 32-bit ARGB icons preserve true alpha; legacy masked icons have opaque alpha assigned to content pixels, eliminating black/transparent box artifacts.

5. **Dual-Key Caching & Bounded Concurrency**:
   - Dual-keyed `ConcurrentDictionary` by window source ID and `exe_{ProcessPath.ToLowerInvariant()}`, reusing process-level icons across multiple windows (e.g. multi-window Chrome or Explorer) in 0ms.
   - [ViewModels/SourcesViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SourcesViewModel.cs) `LoadIconsAsync` upgraded with `SemaphoreSlim(8)` to load icons in parallel without blocking the UI thread.

---

## 3. Files Modified

### Modified Files
- [Services/Win32WindowDiscoveryService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32WindowDiscoveryService.cs)
- [Services/Win32WindowIconService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32WindowIconService.cs)
- [ViewModels/SourcesViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SourcesViewModel.cs)
- [SwitchCast.Tests/Services/WindowIconServiceTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/WindowIconServiceTests.cs)
- [SwitchCast.Tests/Stubs/XamlStubs.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Stubs/XamlStubs.cs)
- [.agents/PROJECT_STATE.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/PROJECT_STATE.md)
- [.agents/HANDOFF.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/HANDOFF.md)
- [.agents/CHANGELOG.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/CHANGELOG.md)

---

## 4. Validation Performed
- **Level 1 (Build)**: `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> PASS (0 warnings, 0 errors in 2.51s).
- **Level 2 (Static Analysis)**: Analyzers and nullable reference checks -> PASS (0 warnings).
- **Level 3 (Unit Tests)**: `dotnet test SwitchCast.Tests\SwitchCast.Tests.csproj -c Debug` -> PASS (156 passed, 0 failed, 0 skipped in 434ms).
- **Level 4 (Dynamic Application Icons & Real Windows Extraction)**: Verified real `explorer.exe` icon extraction and process-level cache reuse in automated test suite.

---

## 5. Next Steps
- **Next Task**: **Phase 6 — Stability & Performance Optimization**
- Implement Direct3D 11 device loss resilience, dynamic multi-monitor DPI scaling adaptation, and extended load verification.



