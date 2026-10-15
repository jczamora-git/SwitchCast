# SWITCHCAST — LOCAL PRESENTATION EXPANSION PLAN

**Project Management & Technical Implementation Plan**  
**Planning version:** 0.1 | **Baseline:** SwitchCast v1.2.1 | **Date:** October 15, 2026  
**Prepared for:** SwitchCast Product & Technical Team | **Owner:** John Christopher King Zamora  
**Status:** Proposal — not implemented or release-approved

## 1. Executive summary

SwitchCast already provides a shareable, stable Presentation Output window, native window/display capture, image/video sources, mixed queue navigation, Floating Presenter Dock, three switching modes, media controls, global shortcuts, and local portable Windows x64 distribution. This plan extends those capabilities for classrooms, seminars, meetings and hybrid events without changing the application's local-first design.

**Goal:** Add PowerPoint presentations and local camera devices as first-class sources while automatically directing the existing Presentation Output to an extended TV/projector when enabled. All three additions must preserve the same Presentation Output instance and existing offline workflows.

**Delivery strategy:** Implement monitor automation first (low architectural risk), then static PowerPoint, then native PowerPoint Live, then camera sources, followed by cross-source and physical-hardware regression testing. Do not bundle the integrations into one unreviewed change.

## 2. Current baseline, gaps and rationale

| Area | Existing capability (v1.2.1) | Gap | Why address it |
|---|---|---|---|
| Application/monitor | Native Windows Graphics Capture | No PowerPoint-specific deck handling | Users must manually manage PowerPoint slideshow windows |
| Media | Direct images/video playback | No PPTX source, slide counts, slide commands | Cannot queue a complete slide deck as one source |
| Output | Windowed and fullscreen; fixed HWND | No automatic extended monitor destination | Presenter must drag output and toggle fullscreen |
| Sources | Window, Display, Image, Video | No direct webcam/USB camera source | Requires separate camera preview application |
| Dock | Adaptive video transport controls | No slide-specific controls | Speaker cannot change slides from the dock |
| Stability | Existing capture/media lifecycle and regression tests | New APIs create device/process/hotplug failure paths | Protect presentations from crashes, wrong focus and leaked devices |

**Source of truth:** Existing v1.2.1 repository and published release. Project roadmap descriptions of future features must not be misrepresented as implemented.

## 3. Business requirements and success measures

| ID | Requirement | Priority | Acceptance metric |
|---|---|---|---|
| LP-01 | Import PPTX as a single queue item | Must | Deck loads or displays actionable error |
| LP-02 | Next/Previous slide + current slide display | Must | Dock controls track rendered slide correctly |
| LP-03 | Optional native PowerPoint animations/transitions | Should | Supported deck runs with PowerPoint installed |
| LP-04 | Detect extended screen and offer auto presentation destination | Must | Exactly 2 extended displays selects non-primary |
| LP-05 | Auto fullscreen setting with manual override | Must | OFF disables all automatic relocation/fullscreen |
| LP-06 | Safe projector hotplug/fallback | Must | No crash, no automatic fullscreen on laptop |
| LP-07 | Discover built-in/USB cameras | Must | User can select an available camera device |
| LP-08 | Preview and take camera live | Must | Camera occupies existing output without external app |
| LP-09 | Camera permission, device busy, unplug handling | Must | Helpful error, clean release, no unintended capture |
| LP-10 | Mixed source queue across six source categories | Must | Next/Previous, pause, blackout, stop remain reliable |
| LP-11 | Direct camera audio and multi-camera composition | Later | Explicitly excluded from first release |

## 4. Phase P0 — Readiness and architecture gate

**Objective:** Prevent regression of existing presenter flow.

**Work:** Inventory coordinator, capture, media playback, output window/display mode, Dock control/popup, session generations, settings JSON, hotkeys and lifecycle; document exact new interfaces. Reproduce local smoke tests, especially rapid Window→Video→Image switching and playback-slider UI thread affinity. Specify new source model contracts while retaining IDs, On Air versus selected state, and latest-request-wins serialization.

**Deliverables:** Architecture decision record, test baseline, source-type contract, UI flow map, release branch plan.

**Gate:** Existing Debug/Release x64 build and automated tests pass; no known blocking runtime crash in the local hardware test matrix. Automated tests alone are insufficient.

**Estimated engineering effort:** 2–4 working days.

## 5. Phase P1 — Smart External Display Mode

**Objective:** Make laptop-to-projector use simple and reversible.

**User workflow:** Connect TV/projector → choose Windows Extend mode → launch/open Presentation Output → if enabled and there is one non-primary display, move the existing output to that display and enter fullscreen. Dashboard and Dock remain on primary display. Manual output monitor selection is always available.

**Settings proposal:**
- `Auto-detect external display` — ON by default (proposed; validate UX with users).
- `Automatically fullscreen on external display` — ON only when auto-detect is enabled; independent manual Fullscreen control remains.
- `Preferred presentation display` — Auto / Display 1 / Display 2 / additional displays.
- `Restore previous manual output position when automation is disabled` — behavior to be tested, not assumed.

**Technical implementation:** Use WinUI `DisplayArea.FindAll()` and `DisplayArea.CreateWatcher()` on the UI thread, supported in Windows App SDK 1.5. Reconcile display identity and topology; debounce Added/Removed/Updated events. Use the existing `AppWindow` and `PresentationWindowService` to move before toggling the existing fullscreen presenter. Preserve the exact HWND and all capture/playback state. Persist user preferences in `settings.json`, without hardcoding monitor indexes.

**Behavior policy:**
- 1 display or Windows Duplicate mode: remain windowed/current manual mode; do not claim an extended projector exists.
- Exactly 2 extended displays: choose non-primary when Auto is enabled.
- 3+ displays: reuse confirmed preferred display; otherwise require explicit selection (do not guess).
- External screen connected mid-presentation: perform automatic relocation only if the setting is enabled and no manual override for the current session; avoid repeated switching.
- External screen disconnected: exit fullscreen if needed and restore output safely to an available display, preferably windowed on primary. Never fullscreen the private laptop screen as an emergency fallback.
- Settings OFF: no automatic move or fullscreen, even after restart/hotplug; native manual features continue.

**Acceptance:** Works for HDMI, USB-C/DisplayPort, mixed DPI/scaling, 1080p/4K, unplug/replug, restart, manual disable, and output window already shared to meeting. No output recreation.

**Estimated effort:** 4–7 working days.

## 6. Phase P2 — PowerPoint Static Slide Source

**Objective:** Allow a PPTX to behave like a first-class queued media presentation without requiring a live slideshow window during playback.

**Features:** Import `.pptx` (optional `.ppt` conversion later), read deck metadata, generate slide images, show thumbnail/title/slide count, display current slide in Presentation Output, provide First/Previous/Next/Last and jump-to-slide. Preserve aspect ratio. Persist the source file path, not raw rendered slides, and invalidate render cache when file changes.

**Rendering decision:** A PPTX is not an image/video. Use a separately evaluated rendering provider (e.g., attended PowerPoint export if installed, or validated document-conversion engine) and keep it behind `IPresentationSlideRenderer`. Do not pretend Open XML alone guarantees faithful slide rendering. No PowerPoint installation during presenting only if slide images are already rendered successfully.

**Limits:** Static mode omits live animations, embedded videos, interactive objects and full transition behavior. Fonts and layout can differ based on renderer. Documents with macros or external links require careful handling; never auto-enable macros.

**Acceptance:** 10-, 50- and 100-slide decks load; slide order, quality, aspect ratio, empty state, re-import and missing file recovery work. Navigation affects slides, not SwitchCast source queue.

**Estimated effort:** 1–2 weeks (renderer selection is a feasibility gate).

## 7. Phase P3 — PowerPoint Live Source

**Objective:** Preserve PowerPoint-native transitions, animation steps and embedded media when installed PowerPoint is available.

**Features:** `PowerPoint Live` option, presentation state tracker, slide/show controls (Next/Previous/Go To), slide count, end-of-show behavior, animated slide events, and safe slideshow start/stop. Use Microsoft PowerPoint's supported interactive COM object model, including `SlideShowSettings.Run()` and `SlideShowWindow.View`. Execute COM work on the appropriate STA/apartment, with deliberate timeouts and marshaling.

**Critical display collision:** A normal full-screen PowerPoint slideshow can occupy the same external monitor as SwitchCast Presentation Output. Avoid this by testing a WINDOWED PowerPoint slideshow on the presenter display and capturing its window, while SwitchCast alone remains fullscreen on the projector. Do not hide/minimize PowerPoint if that interrupts Windows Graphics Capture. Alternative pipelines require independent proof of concept; do not promise invisible rendering.

**Limits:** Installed/licensed desktop PowerPoint is needed for this pathway. COM availability/version, Protected View, security prompts, slideshow focus, media audio, font availability and Windows capture restrictions affect reliability. Microsoft does not recommend unattended/server-side Office automation; the integration is for attended user desktop sessions, not SwitchCast cloud bots.

**Acceptance:** Live animated deck is visible through the same SwitchCast output; separate queued source navigation does not accidentally skip slides; user closes PowerPoint without crashing SwitchCast; projector fullscreen remains stable.

**Estimated effort:** 2–3 weeks after feasibility proof.

## 8. Phase P4 — Camera Source Integration

**Objective:** Add webcam/camera as a native local source alongside existing windows/displays/images/videos.

**MVP camera scope:** Enumerate integrated laptops cameras, USB webcams and video-capture devices that Windows exposes as camera capture endpoints. Show device friendly name and status, add/queue/unqueue, selected-camera preview, take live, flip/mirror (optional), aspect ratio letterboxing, and recover on unplug/device contention. Prefer one active camera at a time initially. Camera is full-frame, not a picture-in-picture overlay.

**Technical implementation:** Introduce `CameraSource` keyed by stable device ID. Use `DeviceInformation` for discovery and Windows `MediaCapture` (with capture failure handling). WinUI 3 camera preview is supported through `MediaFrameSource`→`MediaSource`→`MediaPlayerElement`; `CaptureElement` is not available in WinUI 3. For integration with existing capture rendering, isolate `ICameraCaptureService` and evaluate frame-reader/texture delivery versus a dedicated visual layer. Do not create a second media playback ownership conflict.

**Privacy and limitations:** Capture only after an explicit user action or live camera activation, show active-camera indicator, stop/dispose on source switch or session stop, respect Windows camera privacy, handle `camera in use`/exclusive access and driver variability. No microphone recording or audio sharing in camera MVP. Virtual cameras and professional HDMI capture interfaces depend on the device exposing a compatible Windows video capture interface; validate individually.

**Acceptance:** Camera preview/live on integrated and USB cameras, switch Camera→Window→Video→Camera repeatedly, recover unplug/replug, respect deny permissions, no device lock after Stop, no stale background camera capture.

**Estimated effort:** 1–2 weeks.

## 9. Phase P5 — Unified UI, cross-feature integration and QA

**Sources page:** Add `PowerPoint` and `Camera` categories alongside existing Windows, Displays and Media. One authoritative ordered queue across all categories. Requeue/unqueue works from Dashboard and Floating Dock.

**Floating Dock:** Top row Next/Previous always navigates QUEUED SOURCES. Bottom adaptive row depends on On Air type: Video=transport timeline; PowerPoint=slide controls; Camera=no transport row (camera controls via small overflow panel). Avoid using the same arrow icon semantics for source navigation and slide navigation without visual separation.

**Three switch modes:** Live Only takes selected source live without changing focus. Active + Live takes it live then focuses the relevant interactive source/output. Active Only updates selection/focus without changing audience-facing On Air content. For static slides and camera, focus Presentation Output; PowerPoint Live may need interactive focus behavior explicitly documented. All modes must preserve On Air/Selected separation.

**Hardware regression matrix:** 1 monitor; 2 extended monitors; 3 monitors; projector hotplug; mixed DPI; fullscreen; PowerPoint static deck; PowerPoint live deck with animations; built-in camera; USB webcam; denied/busy webcam; media video; rapid mixed-source navigation; pause; blackout; stop; dock close; app shutdown; resume after device changes.

**Release gate:** Unit/integration tests, actual native x64 manual tests, no new COM wrong-thread crash, no capture/resource leaks, no accidental projector/laptop privacy exposure, accurate docs and release packaging. Record hardware and PowerPoint version tested.

**Estimated effort:** 1–2 weeks.

## 10. Priority, dependencies and release packages

| Delivery package | Included phases | User value | Release gate |
|---|---|---|---|
| Local Display UX | P0 + P1 | One-click TV/projector presentation | Hotplug and output HWND tests |
| Slide Presentation | P2 | Deck as one queued source | Renderer fidelity and slide navigation |
| Live PowerPoint | P3 | Native animation and presenter controls | COM and display collision proof |
| Camera Presentation | P4 | Direct live camera as source | Permission, busy and unplug recovery |
| Consolidated Local Presentation | P5 | Reliable integrated experience | Full native regression matrix |

Engineering recommendation: deliver P1 early; do not make P3 or P4 prerequisites for shipping P1. Keep the PowerPoint static feature useful even if Live integration is deferred.

## 11. Risk register

| Risk | Impact | Mitigation / gate |
|---|---|---|
| PowerPoint slideshow takes over projector | High | Use tested windowed show on presenter monitor; prohibit competing fullscreen output |
| Different fonts/layout in PPTX render | Medium | Fidelity tests, user warning, alternative Office-based render |
| Office COM deadlock/focus changes | High | Attended desktop only, STA ownership, safe cancellation and cleanup |
| Display hotplug during active meeting | High | Display identity, debounce, manual override, fallback to safe windowed display |
| Webcam denied/busy/unplugged | Medium | Device-state events, diagnostics, explicit disposal |
| GPU load from concurrent preview/camera/video | High | Only necessary active capture, throttled preview, instrumentation |
| Queue/source index desynchronization | High | Unified source IDs/state, regression tests across 6 categories |
| Confusing Dock source-vs-slide navigation | Medium | Separated rows, clear tooltips/labels and hotkeys |

## 12. Proposed ownership and team decisions

**Product Owner:** Approves requirements, expected user flow, priority and display-mode defaults. **Windows/WinUI Engineer:** Implements output routing, Settings, Dock and lifecycle. **Media Engineer:** Evaluates PPTX rendering, PowerPoint interop and camera acquisition. **QA:** Verifies device matrix and meeting/projector cases. One person can hold multiple roles in a small team.

**Decision points before implementation:**
1. Confirm default for Auto-detect ON/OFF, and whether a pop-up is wanted on first external display connection.
2. Confirm whether static PPTX support must work without PowerPoint installed, which determines the slide-rendering engine.
3. Confirm PowerPoint Live requirement: native animation steps/embedded media or slides only.
4. Confirm camera priority: full-screen camera only for MVP; picture-in-picture later.
5. Select representative projector, camera models, and PowerPoint versions for acceptance testing.

## 13. Future items explicitly outside this delivery

Online SwitchCast Sessions, remote source streaming, delegated permissions, remote desktop control, hosted SwitchCast Stage, meeting presentation bots, multi-camera compositing, camera PiP, microphone mixing, PowerPoint cloud rendering, and unattended Office automation. These remain in the separate remote-sessions PM roadmap.

## 14. Source references and assumptions

- SwitchCast v1.2.1 GitHub source and release: https://github.com/jczamora-git/SwitchCast/releases/tag/v1.2.1
- Microsoft DisplayArea display list and watcher: https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.displayarea.findall and https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.displayarea.createwatcher
- PowerPoint interactive slideshow: https://learn.microsoft.com/en-us/office/vba/api/powerpoint.slideshowsettings.run and https://learn.microsoft.com/en-us/office/vba/api/powerpoint.slideshowview.next
- Microsoft Office unattended automation warning: https://learn.microsoft.com/en-us/office/client-developer/integration/considerations-unattended-automation-office-microsoft-365-for-unattended-rpa
- WinUI 3 camera preview: https://learn.microsoft.com/en-us/windows/apps/develop/camera/simple-camera-preview-access

## 15. Implementation work packages and pull request sequencing

| Work package | Suggested engineering unit | Verification artifact |
|---|---|---|
| WP-01 | Display watcher, topology and preference model | Unit tests for monitor choice, hotplug and OFF behavior |
| WP-02 | Output relocation and auto-fullscreen orchestration | Physical projector demo; same HWND evidence |
| WP-03 | PPTX import model and rendering provider | Golden slide image comparison and metadata tests |
| WP-04 | Static slide playback and dock navigation | End-to-end deck control demonstration |
| WP-05 | PowerPoint attended COM integration | Windowed slideshow capture and event stress test |
| WP-06 | Device discovery and CameraSource model | Built-in/USB camera device list tests |
| WP-07 | Camera preview/output/lifecycle | Permission, unplug, busy-device runtime test |
| WP-08 | Unified switching, settings and release QA | Full cross-source hardware regression report |

Keep these independently reviewable. After each work package, update the user-facing feature documentation and `.agents/PROJECT_STATE.md`, `.agents/HANDOFF.md`, and `.agents/CHANGELOG.md`; do not rely solely on historical changelog notes to explain product behavior.

## 16. Definition of done and approval log

A feature is considered deliverable only when requirements are traced to code, settings defaults are documented, first-run and failure states are usable, all relevant automated tests pass, native hardware scenarios are demonstrated, and no blocker remains for capture, media or presentation window continuity.

| Review gate | Required sign-off | State |
|---|---|---|
| Scope baseline / prioritization | Product Owner | Pending |
| PowerPoint renderer and Office dependency | Technical Lead | Pending |
| Projector automatic relocation behavior | Product + QA | Pending |
| Camera capture privacy and permissions | Engineering + QA | Pending |
| Consolidated native regression and release | Release Owner | Pending |

**Planning note:** This is a proposed integration design, not a validation report. Effort estimates depend on developer availability, selected rendering technology, camera hardware and Office integration feasibility. Existing SwitchCast source-of-truth files should be rechecked before implementation if the local repository is ahead of v1.2.1.
