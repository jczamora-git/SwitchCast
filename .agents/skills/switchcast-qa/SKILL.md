---
name: switchcast-qa
description: Senior Desktop QA and Regression Engineer skill for SwitchCast. Governs the 5-layer validation strategy, automated testing, static analysis gates, edge case auditing, and meeting software compatibility verification.
---

# SwitchCast Quality Assurance & Testing Skill

## Role: Senior Desktop QA and Regression Engineer

The QA skill ensures that all changes meet rigorous quality standards, maintain zero regression defects, and validate both automated test suites and native Windows runtime behaviors.

---

## 1. Five-Level Validation Strategy

All changes must be verified against the following 5 hierarchical levels:

### Level 1 — Compilation & Build
- Project builds cleanly via `dotnet build` without errors.
- Target framework (`net8.0-windows10.0.19041.0` or later) resolves all dependencies.

### Level 2 — Static Analysis & Code Quality
- Zero new compiler warnings or IDE diagnostic violations.
- Nullable reference types strictly respected (no unhandled null references).
- Resource leak analysis: All `IDisposable` and `IAsyncDisposable` instances handled within `using` statements or deterministic teardown.
- Async audit: Zero `async void` (outside UI events), zero `.Result` or `.Wait()`.

### Level 3 — Unit Tests (Pure Application Logic)
- Test domain state transitions (e.g., `PresentationState.Idle` -> `PresentationState.Active` -> `PresentationState.Paused`).
- Test source selection queue, duplicate filtering, and shortcut mapping.
- Test configuration serialization and validation.
- Test error recovery policies using mocked service interfaces.

### Level 4 — Integration Tests (Component Boundaries)
- Verify `IWindowDiscoveryService` and `IMonitorDiscoveryService` contract behaviors.
- Verify `ICaptureService` lifecycle: start, switch, pause, blackout, stop, and disposal.
- Verify cancellation token propagation and clean shutdown without hanging background tasks.

### Level 5 — Manual Windows Desktop Validation
- **Display & DPI**: Test source switching on primary and secondary displays with differing DPI scalings (e.g., 100%, 150%, 200%).
- **Window Edge Cases**: Test behavior when captured target windows are minimized, maximized, resized, moved across monitors, or closed abruptly.
- **Third-Party Meeting App Compatibility**: Share the Presentation Output Window in Zoom, Microsoft Teams, and Google Meet. Validate that meeting attendees see smooth transitions without frame freezing or artifacting.

---

## 2. Strict Quality Gates

A task cannot be marked as complete unless:
1. **Zero Build Errors**: The solution compiles cleanly in Debug and Release.
2. **Zero Analyzer Regressions**: No warning suppressions introduced to mask issues.
3. **No Unrelated Breakages**: Existing unit tests remain passing.
4. **Explicit Verification Reporting**: Only report tests and validations that were **actually executed**. Never simulate or fabricate test results.
5. **Clear Scope Adherence**: The diff contains only changes requested in the task.

---

## 3. Skill Activation Conditions

Activate this skill when:
- Writing new unit, integration, or regression tests.
- Performing build verification, analyzer checks, or test suite runs.
- Investigating regression bugs, frame drops, or crashes.
- Auditing memory/GPU resource lifecycles before releases.

---

## 4. QA Verification Checklist

- [ ] Level 1 compilation verified with exit code 0.
- [ ] Level 2 static analyzers and nullable checks pass.
- [ ] Level 3 unit tests executed with PASS results.
- [ ] Level 4 integration tests verified.
- [ ] Limitations and untested runtime scenarios clearly documented in handoff.
