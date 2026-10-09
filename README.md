# SwitchCast

> **Seamless Windows Screen Sharing & Presentation Management**  
> Capture applications, displays, images, and videos, queue your presentation sources, and switch seamlessly in a single stable output window without interrupting your meeting stream.

[![Release](https://img.shields.io/badge/Release-v1.2.1-FF7A59?style=flat-square)](https://github.com/jczamora-git/SwitchCast/releases)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-blue?style=flat-square)](https://github.com/jczamora-git/SwitchCast)
[![Framework](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square)](https://dotnet.microsoft.com/)
[![UI](https://img.shields.io/badge/UI-WinUI%203%20%2F%20Windows%20App%20SDK-0078D7?style=flat-square)](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)
[![License](https://img.shields.io/badge/Privacy-100%25%20Offline%20%26%20Local-2ea44f?style=flat-square)](https://github.com/jczamora-git/SwitchCast)

---

## Overview

During virtual presentations and live screen-sharing sessions (Zoom, Microsoft Teams, Google Meet, Discord, OBS), presenters frequently need to transition between slide decks, IDE code editors, terminal windows, browser tabs, media assets, and application demos. Standard conferencing tools require stopping screen sharing, selecting another window or tab, and restarting screen sharing—disrupting the meeting flow and risking accidental exposure of private desktop content.

**SwitchCast solves this problem.**

Presenters share one dedicated, stable **Presentation Output** window in their meeting software. Inside SwitchCast, presenters queue multiple applications, monitors, images, or video files and switch between them instantly using the **Control Dashboard**, the lightweight **Floating Presenter Dock**, or **Global Keyboard Shortcuts**—all while the meeting audience sees a continuous, uninterrupted live presentation feed.

---

## Key Features

- **Live Window & Monitor Capture**: Hardware-accelerated desktop capture via `Windows.Graphics.Capture` and Direct3D 11.
- **Media Presentation (Images & Videos)**: Import PNG, JPG, BMP, GIF images and MP4, MKV, MOV, WMV, AVI video files directly into the presentation queue with seamless rendering and zero format transcoding.
- **Direct Video Audio Playback & Volume Controls**: High-fidelity local video audio playback with atomic audio/video seek synchronization, volume slider (0–100%), and instant mute toggles.
- **Dedicated Fullscreen & Windowed Presentation Output**: Clean 16:9 audience-facing presentation canvas titled `"SwitchCast Presentation Output"`, supporting both standard windowed mode and native borderless fullscreen mode (`AppWindowPresenterKind.FullScreen`).
- **Instant Seamless Source Switching**: Switch between queued application windows, physical displays, and media files in milliseconds with latest-request-wins concurrency serialization.
- **Three Source Switching Modes**:
  - **Live Only (`L`)** *(Default)*: Switches the shared output stream without changing your active desktop focus.
  - **Active + Live (`A+L`)**: Switches the shared output stream and simultaneously brings the selected application window to the foreground.
  - **Active Only (`A`)**: Brings the application to your desktop foreground for private interaction without altering the live audience feed.
- **Floating Presenter Companion Dock**: Ultra-slim, always-on-top draggable floating toolbar with native Windows non-client caption dragging, live timecode scrub slider, transport controls (Restart, Backward 10s, Play/Pause, Forward 10s), switching mode selector, and fullscreen toggle.
- **System-Wide Global Hotkeys**: Direct shortcut control across Windows even when SwitchCast is minimized or in the background.
- **Live Presentation Controls**: Instant **Pause / Freeze-Frame** and 100% opaque **Blackout / Privacy Curtain** controls.
- **Dynamic True-Color Native Icons**: Automatically extracts and displays authentic 32-bit Windows application icons (Chrome, Visual Studio, File Explorer, IDEs) in the Sources picker.
- **Modern Fluent Design Shell**: Custom dark title bars, DPI-aware geometry centering, theme synchronization (Dark & Light modes), and Windows 11 Snap Layouts.
- **Safe Window Lifecycle & Exit Confirmation**: Prevents accidental meeting terminations with clear prompts, independent secondary window lifecycle, and clean resource teardown.
- **100% Privacy & Zero Telemetry**: Operates entirely offline on your local device. No network requests, analytics, or cloud dependencies.

---

## Technology Stack

- **Language & Runtime**: C# / .NET 8.0 (`net8.0-windows10.0.19041.0`)
- **UI Framework**: WinUI 3 (Windows App SDK 1.5)
- **Capture Engine**: `Windows.Graphics.Capture` (WinRT Interop) & Direct3D 11 swapchain pipelines
- **Media Pipeline**: `Windows.Media.Playback.MediaPlayer` & `MediaPlayerElement`
- **Diagnostic Capture**: Win32 GDI `PrintWindow` (`PW_RENDERFULLCONTENT`) and `BitBlt` fail-safe fallbacks
- **Architecture**: Clean Architecture MVVM (`CommunityToolkit.Mvvm`, `Microsoft.Extensions.DependencyInjection`)
- **Target OS**: Windows 10 (version 1809 / build 17763 or later) & Windows 11 (x64)

---

## Getting Started

### System Requirements
- **Operating System**: Windows 10 64-bit (version 1809 or higher) or Windows 11 64-bit
- **Architecture**: x64
- **Graphics**: Direct3D 11 compatible graphics hardware or WARP software rasterizer

### Installation

1. Download the latest `SwitchCast-v1.2.1-win-x64.zip` release from [Releases](https://github.com/jczamora-git/SwitchCast/releases).
2. Extract the archive to any folder on your computer.
3. Run `SwitchCast.exe`.

*Note: SwitchCast is configured as a standalone, unpackaged desktop application. No installation of external .NET runtimes is required.*

---

## Basic Usage

1. **Discover & Queue Sources**:
   - Open SwitchCast and navigate to the **Sources** tab.
   - Check the boxes next to the application windows, displays, images, or video files you want to include in your presentation.
2. **Open Presentation Output**:
   - Click **Open Output Window** from the Dashboard or Presenter Dock.
3. **Share in Meeting**:
   - In Zoom, Microsoft Teams, or Google Meet, choose **Share Window** and select **"SwitchCast Presentation Output"**.
4. **Start Presenting**:
   - Click **Start Presenting** on the Dashboard or Presenter Dock.
5. **Switch Between Sources & Control Media**:
   - Use the Floating Presenter Dock, Dashboard, or **Global Hotkeys** (`Ctrl+Shift+Right` / `Ctrl+Shift+Left`) to switch smoothly between your queued sources.
   - Control video playback (Seek, Scrub timeline, Play/Pause, Mute/Volume) directly from the dock.
6. **Toggle Fullscreen**:
   - Click the Fullscreen button on the dock or press `Escape` inside the output window to toggle borderless fullscreen.

---

## Default Keyboard Shortcuts

| Action | Default Shortcut | Description |
| :--- | :--- | :--- |
| **Next Presentation Source** | `Ctrl + Shift + →` | Switches to the next source in the presentation queue |
| **Previous Presentation Source** | `Ctrl + Shift + ←` | Switches to the previous source in the presentation queue |
| **Direct Source 1–5** | `Ctrl + Shift + 1..5` | Instantly switches to the corresponding queued source |
| **Toggle Pause / Resume** | `Ctrl + Shift + P` | Freezes or resumes the live presentation feed |
| **Toggle Blackout** | `Ctrl + Shift + B` | Blanks the output canvas for instant presenter privacy |
| **Stop Presenting** | `Ctrl + Shift + S` | Safely stops live presentation and returns output to standby |
| **Toggle Presenter Dock** | `Ctrl + Shift + D` | Shows or hides the floating companion toolbar |
| **Focus Dashboard** | `Ctrl + Shift + M` | Restores and brings the main SwitchCast window forward |

*Shortcuts can be viewed and configured in the **Settings > Shortcuts** panel.*

---

## Known Platform Behaviors

- **Minimized Application Windows**: In accordance with standard Windows OS graphics compositor policy, minimized application windows suspend DirectX frame generation. SwitchCast retains the last captured frame until the window is restored.
- **OS Protected Windows**: Windows with elevated DRM or display affinity flags (`WDA_MONITOR` / `WDA_EXCLUDEFROMCAPTURE`) cannot be captured by third-party software by design.
- **Conferencing Audio Sharing**: Local video playback audio is routed to your default Windows audio playback device. To share audio with meeting attendees in conferencing tools (such as Zoom or Teams), select "Share system audio" in your meeting application's share dialog.

---

## Creator & Attribution

Created by **John Christopher King Zamora**  
- **GitHub**: [@jczamora-git](https://github.com/jczamora-git)  
- **Repository**: [https://github.com/jczamora-git/SwitchCast](https://github.com/jczamora-git/SwitchCast)

---

## Releases

Official builds and release packages are published on the [GitHub Releases](https://github.com/jczamora-git/SwitchCast/releases) page.

