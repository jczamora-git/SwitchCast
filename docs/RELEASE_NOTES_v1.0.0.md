# SwitchCast v1.0.0 — Official Windows Release

**Release Date**: 2026-10-09  
**Platform**: Windows x64 (Windows 10 version 1809+ / Windows 11)  
**Creator**: John Christopher King Zamora  
**Repository**: [https://github.com/jczamora-git/SwitchCast](https://github.com/jczamora-git/SwitchCast)

---

## Highlights & Features

SwitchCast is a high-performance, privacy-first desktop application designed for presenters, educators, software engineers, and remote professionals.

### 1. Dedicated Presentation Output Canvas
- Share a single, dedicated **"SwitchCast Presentation Output"** window in Zoom, Microsoft Teams, Google Meet, or Discord.
- No need to stop and restart screen sharing when switching between different applications, browsers, IDEs, or presentation slides.
- Fluid 16:9 presentation canvas with letterbox/pillarbox aspect preservation, live status badges, and pause indicators.

### 2. Multi-Source Presentation Queue & Instant Switching
- Discover and queue active application windows and physical monitors.
- Seamlessly transition between queued sources in milliseconds with Latest-Request-Wins request serialization.

### 3. Three-Mode Source Switching
- **Live Only (`L`)** *(Default)*: Updates the shared presentation stream while leaving your desktop workspace focus untouched.
- **Active + Live (`A+L`)**: Updates the presentation stream and automatically brings the target application window to the foreground.
- **Active Only (`A`)**: Activates the target window on your desktop for presenter interaction without changing the live presentation feed.

### 4. Floating Presenter Companion Dock
- Ultra-slim floating toolbar that stays pinned above full-screen apps and browsers.
- Provides one-click on-air status, quick source switching, pause/blackout toggles, and mode configuration.
- Supports both Expanded and Compact single-row display formats.

### 5. System-Wide Global Hotkeys
- `Ctrl + Shift + →` / `Ctrl + Shift + ←`: Next / Previous Presentation Source
- `Ctrl + Shift + 1..5`: Direct Queued Source Jump
- `Ctrl + Shift + P`: Toggle Pause / Resume
- `Ctrl + Shift + B`: Toggle Blackout / Privacy Screen
- `Ctrl + Shift + S`: Stop Presenting
- `Ctrl + Shift + D`: Toggle Presenter Dock
- `Ctrl + Shift + M`: Focus SwitchCast Dashboard

### 6. Dynamic True-Color Native Icons
- Automatic extraction and rendering of authentic 32-bit Windows application icons (Chrome, Visual Studio, File Explorer, Terminal, IDEs).

### 7. Modern Fluent Design Shell
- Custom dark title bar integration with Windows 11 Snap Layouts.
- Two-pane settings panel, DPI-aware startup centering, and dynamic dark/light theme switching.

### 8. 100% Offline Privacy & Zero Telemetry
- All frame processing and Direct3D texture management occurs strictly in local GPU/CPU memory. No data is stored or transmitted over any network.

---

## Installation & Requirements

### System Requirements
- Windows 10 (version 1809 / build 17763 or later) or Windows 11
- 64-bit (x64) processor
- DirectX 11 compatible graphics adapter

### Running SwitchCast
1. Download `SwitchCast-v1.0.0-win-x64.zip`.
2. Extract the archive into a folder of your choice.
3. Launch `SwitchCast.exe`.
