# SWITCHCAST — SECURITY & PRIVACY MODEL

---

## 1. CORE PRIVACY & SECURITY PHILOSOPHY

SwitchCast is designed with an uncompromising **Privacy-First** and **Zero-Telemetry** architecture. Screen sharing and desktop capture utilities inherently handle highly sensitive user data (passwords, private messages, intellectual property, internal documents). SwitchCast guarantees that captured content never leaves the presenter's local machine.

---

## 2. PRIVACY & SECURITY MANDATES

1. **Local-Only Processing**: All capture frames, texture transfers, scaling calculations, and swapchain presentations occur exclusively inside local GPU and CPU memory.
2. **Zero Remote Telemetry & Tracking**: The application contains no analytics SDKs, telemetry probes, crash reporters sending remote payloads, or tracking pixels.
3. **No Unrequested Persistence**: Captured screen frames are never written to disk or stored in temporary file directories.
4. **Standard Privileges Only**: SwitchCast runs entirely under standard user account privileges and does not require or request administrator elevation (`UAC`).

---

## 3. THREAT MODEL & ATTACK VECTOR MITIGATIONS

```
+--------------------------------------------------------------------------------+
|                             THREAT MITIGATION MATRIX                           |
+-----------------------------------+--------------------------------------------+
| Threat Vector                     | Architectural Mitigation                   |
+-----------------------------------+--------------------------------------------+
| Unintended Dashboard Exposure     | WDA_EXCLUDEFROMCAPTURE on Dashboard HWND   |
| Frame Cross-Contamination         | Backbuffer cleared to black on every switch|
| Protected Content / DRM Bypass   | Respects OS-enforced Windows capture flags |
| Diagnostic Log Exfiltration       | Strict log sanitization (no titles/pixels) |
| Native Memory Corruption          | Safe WinRT wrappers & deterministic dispose|
+-----------------------------------+--------------------------------------------+
```

### A. Dashboard & Notes Isolation
Presenters often view private notes, incoming chats, or dashboard controls. To prevent these from being accidentally shared:
- The Control Dashboard window is configured with `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)`.
- Discovery algorithms strictly exclude the SwitchCast Presentation Output Window from the list of capturable application windows.

### B. Frame Cross-Contamination (Fail-Closed)
When switching from a sensitive document (e.g., Financial Report) to another application:
- Direct3D 11 backbuffers are immediately cleared with a solid color before binding the new frame pool.
- If the new source is invalid or fails to produce frames, the presentation pipeline fails closed to a neutral pause/blank screen, preventing residual frame artifacts from persisting.

### C. Protected Content & DRM
SwitchCast strictly complies with Windows OS security policies:
- Applications marked with `WDA_MONITOR` or `WDA_EXCLUDEFROMCAPTURE` are captured as black regions by the OS-level `Windows.Graphics.Capture` subsystem. SwitchCast never attempts to hook or bypass these protections.

---

## 4. LOGGING & DIAGNOSTICS SANITIZATION

All diagnostic and error logs must adhere to strict sanitization rules:
- **Forbidden in Logs**:
  - Raw pixel arrays, bitmaps, base64 textures.
  - Window titles that match sensitive regex patterns (credentials, token values, banking keywords).
  - Personal user directory paths without path normalization.
- **Allowed in Logs**:
  - Process IDs (`PID`), Window Handles (`HWND` in hex), Display IDs.
  - Frame acquisition timestamps and rendering frame rates (FPS).
  - Exception types, stack traces, and HRESULT error codes.
