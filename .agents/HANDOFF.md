# SWITCHCAST — AGENT HANDOFF RECORD

---

## Task Details
- **Task**: Targeted RoGetActivationFactory HSTRING Marshaling Fix & Phase 4 Stabilization
- **Date**: 2026-10-08T20:35:00+08:00 (UTC+8)
- **Status**: Completed

---

## 1. Objective
Fix the runtime `MarshalDirectiveException` (0x80131535) during `RoGetActivationFactory` invocation in `GraphicsCaptureItemFactory.cs`. Ensure correct ABI-compliant `HSTRING` allocation (`WindowsCreateString`), invocation, and deterministic deletion (`WindowsDeleteString`) across both window (`HWND`) and monitor (`HMONITOR`) capture item creation.

---

## 2. Root Cause Analysis
- In .NET 8 / CsWinRT, `[MarshalAs(UnmanagedType.HString)] string` in P/Invoke signatures is unsupported by the .NET runtime P/Invoke marshaler (which previously supported it in legacy .NET Framework WinRT interop).
- Attempting to pass managed `string` with `[MarshalAs(UnmanagedType.HString)]` resulted in:
  `System.Runtime.InteropServices.MarshalDirectiveException: Cannot marshal 'parameter #1': Invalid managed/unmanaged type combination.`

---

## 3. Files Modified

### Interop & Native Capture Engine
- [Services/Capture/Interop/NativeCaptureMethods.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Interop/NativeCaptureMethods.cs)
  - Declared `WindowsCreateString(string, uint, out IntPtr)` P/Invoke.
  - Declared `WindowsDeleteString(IntPtr)` P/Invoke.
  - Updated `RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory)` to accept native `HSTRING` handle `IntPtr`.
- [Services/Capture/GraphicsCaptureItemFactory.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/GraphicsCaptureItemFactory.cs)
  - Added centralized, leak-safe `GetActivationFactory(string classId, Guid iid)` helper method.
  - Allocates `HSTRING` handle via `WindowsCreateString`, invokes `RoGetActivationFactory`, and deterministically frees the `HSTRING` via `WindowsDeleteString` in a `finally` block.
  - Both `CreateForWindow` and `CreateForMonitor` reuse the safe factory helper.

---

## 4. Validation Performed
- **Level 1 (Build)**: `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> PASS (0 warnings, 0 errors in 30.3s).
- **Level 2 (Static Analysis)**: Analyzers and nullable reference checks -> PASS (0 warnings).
- **Level 3 (Unit Tests)**: `dotnet test SwitchCast.Tests\SwitchCast.Tests.csproj -c Debug` -> PASS (57 passed, 0 failed, 0 skipped in 1s).
- **Level 4 (Native Interop)**: `RoGetActivationFactory` P/Invoke contract verified against official Windows SDK / combase.dll ABI specifications.

---

## 5. Next Steps
- **Next Task**: **Phase 5 — Global Hotkeys & Presentation Switching Controls**
- Implement native Win32 `RegisterHotKey` hooks for background keyboard shortcuts (switching between queued sources 1-9, toggle pause, and instant blackout) when SwitchCast is in the background.
