# SWITCHCAST — PRODUCT REQUIREMENTS DOCUMENT (PRD)

---

## 1. PRODUCT VISION & OVERVIEW

**SwitchCast** is a lightweight, high-performance Windows desktop utility designed for presenters, educators, engineers, and remote workers. It enables seamless real-time switching between active application windows and monitors inside a single, dedicated, shareable presentation output window.

### The Problem
During online presentations and meetings (Zoom, Microsoft Teams, Google Meet), presenters frequently need to switch between different applications (e.g., slides, terminal, IDE, browser, documentation). The standard workflows are cumbersome:
- **Sharing the full desktop**: Exposes personal notifications, background chats, and unrelated clutter (privacy risk).
- **Sharing individual windows**: Requires stopping and restarting screen share repeatedly every time a different application is needed (disruptive, slow).

### The Solution
SwitchCast creates two independent interfaces:
1. **Control Dashboard**: A private management window where the presenter chooses which windows/monitors to queue up, views previews, configures hotkeys, and triggers instant source transitions.
2. **Presentation Output Window**: A dedicated, clean, borderless/resizable window that renders the currently active source. The presenter shares *this window once* in their meeting software. When the presenter switches sources on the dashboard or via hotkeys, the output window content changes instantly without dropping the meeting share.

---

## 2. CORE PRINCIPLES

- **Local-First & Offline-First**: No internet required, zero cloud dependency, zero external API calls.
- **Privacy-First**: No telemetry, no background recording, no data collection, standard user permissions only.
- **High Performance & Low Latency**: Hardware-accelerated capture and rendering using Direct3D 11 and `Windows.Graphics.Capture`.
- **Reliable & Stable**: Zero crashes on source destruction, clean fail-closed states on window close/minimize.

---

## 3. MVP FEATURE REQUIREMENTS

All features below are currently **[PLANNED]** for Phase 1 through Phase 5 development:

| ID | Feature | Description | Status |
| :--- | :--- | :--- | :--- |
| **REQ-01** | **Window Discovery** | Enumerate running desktop application windows with valid titles, icons, and process metadata, filtering out invisible/tool windows. | Planned |
| **REQ-02** | **Monitor Discovery** | Enumerate all connected physical and virtual display monitors with bounds and resolution. | Planned |
| **REQ-03** | **Multi-Source Selection** | Allow users to add multiple windows and monitors to an active presentation queue. | Planned |
| **REQ-04** | **Hardware Capture Engine** | Capture the active source in real time using `Windows.Graphics.Capture` and Direct3D 11. | Planned |
| **REQ-05** | **Presentation Output Window** | Dedicated output window rendering the active source with proper aspect-ratio scaling and letterboxing. | Planned |
| **REQ-06** | **Dashboard Switching** | Instant 1-click source switching from the Control Dashboard interface. | Planned |
| **REQ-07** | **Global Hotkey Switching** | Configurable system-wide shortcuts (e.g., `Ctrl+Shift+1..9`, `Ctrl+Shift+Next`) for headless source switching. | Planned |
| **REQ-08** | **Blackout & Pause Controls** | One-key presentation blackout (blank screen) and pause (freeze current frame) controls. | Planned |
| **REQ-09** | **Fail-Closed Source Handling**| Graceful handling when a captured window is closed, minimized, or occluded, rendering an intentional fallback screen instead of crashing. | Planned |
| **REQ-10** | **Presentation Window Isolation**| Ensure the Presentation Output Window and Control Dashboard never capture or mirror each other in a loop. | Planned |

---

## 4. FUTURE ROADMAP FEATURES (Post-MVP)

- **Live Thumbnail Previews**: Real-time small previews of queued sources on the dashboard.
- **Smooth Visual Transitions**: Configurable cross-fade, slide, and cut transitions between sources.
- **Multi-Source Layouts & Picture-in-Picture**: Side-by-side application comparisons or split-screen layouts.
- **Webcam Overlay**: Optional local webcam feed floating on top of captured content.
- **Presenter Annotation & Cursor Highlight**: Highlighting mouse clicks and drawing overlay tools.
- **Presentation Profiles**: Saved queues of applications for recurring presentations.
- **Virtual Camera Output**: Optional DirectShow / Media Foundation virtual camera device feed.

---

## 5. EXPLICITLY OUT OF SCOPE (MVP)

The following capabilities are strictly out of scope for the MVP to prevent bloat and maintain security:
- Video recording to disk (MP4/MKV recording).
- Direct streaming integrations (RTMP to Twitch, YouTube, Kick).
- Cloud accounts, synchronization, or remote collaboration.
- Built-in video conferencing or WebRTC calling.
- Cloud AI features or automated transcriptions.
- Monetization frameworks or telemetry analytics.
