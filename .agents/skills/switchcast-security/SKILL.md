---
name: switchcast-security
description: Windows Desktop Security and Privacy Engineer skill for SwitchCast. Governs screen capture privacy, credential protection, DRM/protected-content adherence, leak prevention, and zero-telemetry enforcement.
---

# SwitchCast Security & Privacy Skill

## Role: Windows Desktop Security Engineer

The Security skill is responsible for guarding user privacy, ensuring zero telemetry, validating local-only frame pipelines, preventing data leaks across window switching boundaries, and enforcing safe desktop interop.

---

## 1. Core Security & Privacy Requirements

All agents and implementations MUST strictly enforce the following 12 security mandates:

1. **Explicit User Initiation**: Screen capture MUST only be initiated through deliberate user action (clicking a source or pressing a designated hotkey). No automated or stealth capture.
2. **Local-Only Frame Pipeline**: Captured frames, textures, and buffers MUST remain exclusively in local system/GPU memory.
3. **Zero Network Transmission**: Never transmit frame data, window titles, process metadata, or user inputs to external or remote servers.
4. **Standard User Privileges**: The application must run under standard user privileges. Do NOT require or prompt for administrator elevation for standard window or monitor capture.
5. **Respect Protected Content**: Strictly respect Windows DRM and protected content flags (`SetWindowDisplayAffinity`). Never attempt to bypass OS-enforced capture restrictions.
6. **No Frame Data in Logs**: Diagnostic logs must NEVER contain raw pixels, decoded image buffers, or sensitive frame metadata.
7. **No Unencrypted Thumbnail Caching**: Window previews/thumbnails must not be written to unencrypted disk caches by default. Thumbnails reside in volatile memory only.
8. **Dashboard Isolation**: The Presentation Output Window must never inadvertently reveal the Control Dashboard, presenter notes, or shortcut lists.
9. **Fail-Closed Cross-Contamination Prevention**: Switching to an unavailable, crashed, or closed source must immediately clear the frame buffer, rendering a blank or paused state rather than retaining old frame contents.
10. **Sanitized Diagnostics**: Application logs must not include credentials, access tokens, URLs with sensitive query strings, or personal data.
11. **Dependency Vetting**: All third-party NuGet dependencies must have documented justifications, active maintenance records, and zero suspicious telemetry hooks.
12. **No Surveillance Capabilities**: SwitchCast is strictly a real-time presentation switcher; it does not include hidden background recording, keystroke logging beyond configured hotkeys, or stealth capture.

---

## 2. Threat Model & Risk Mitigations

| Threat | Impact | Mitigation |
| :--- | :--- | :--- |
| **Accidental Dashboard Exposure** | Presenter's private controls visible in meeting | Mark dashboard with `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` and isolate presentation window. |
| **Residual Frame Leakage** | Stale window content displayed after switch | Explicitly clear the Direct3D backbuffer to solid black/theme background upon source change. |
| **Elevation Vulnerabilities** | Privilege escalation via native interop | Restrict interop to standard desktop Win32/WinRT APIs with standard user rights. |
| **Dependency Hijack** | Malicious telemetry or exfiltration via package | Strict dependency review; prefer built-in .NET 8 / Windows App SDK APIs. |

---

## 3. Skill Activation Conditions

Activate this skill when:
- Designing or modifying frame buffers, caching, or logging mechanisms.
- Adding or modifying native Win32/WinRT API interop.
- Introducing new NuGet packages or third-party libraries.
- Modifying hotkey registration or input handling.
- Reviewing permission requirements or package manifests (`Package.appxmanifest`).

---

## 4. Security Verification Checklist

Before approving code, verify:
- [ ] Are capture buffers isolated from network I/O?
- [ ] Are sensitive window titles and frame pixels excluded from logs?
- [ ] Does switching clear previous frame textures immediately?
- [ ] Is `SetWindowDisplayAffinity` properly utilized where applicable?
- [ ] Does the application run completely without admin privileges?
