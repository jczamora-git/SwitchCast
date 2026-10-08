---
name: switchcast-winui
description: Senior WinUI 3 and C# Desktop Engineer skill for SwitchCast. Governs XAML views, MVVM architecture, CommunityToolkit.Mvvm usage, UI thread safety, DPI scaling, and responsive theming.
---

# SwitchCast WinUI 3 Specialist Skill

## Role: Senior WinUI 3 / C# Desktop Engineer

The WinUI 3 skill governs the desktop user interface, MVVM presentation patterns, XAML controls, theme handling, DPI scaling, and windowing for SwitchCast.

---

## 1. Core Responsibilities

1. **Independent Windows Architecture**:
   - **Control Dashboard (`MainWindow.xaml`)**: Manage source selection, previews, settings, and hotkey configuration.
   - **Presentation Output Window (`PresentationWindow.xaml`)**: Dedicated borderless or clean output window designed to be shared via Zoom/Teams/Meet. Must remain logically isolated from the dashboard.
2. **Strict MVVM Pattern**:
   - Views contain UI markup, animations, and visual states only.
   - ViewModels contain observable properties, commands (`IAsyncRelayCommand`), and presentation logic.
   - Code-behind is strictly limited to View-lifecycle operations (e.g., window sizing, platform interop window handles `HWND`).
3. **UI Thread & Async Safety**:
   - Never block the UI thread with `.Result` or `.Wait()`.
   - Marshal background events/callbacks to the UI thread using `DispatcherQueue` only when updating UI-bound properties.
   - Use `async Task` for asynchronous operations; avoid `async void` except for standard event handler signatures.
4. **Theming, High-DPI, and Accessibility**:
   - Full support for Windows Light and Dark themes via `ThemeResource` brushes.
   - Avoid hardcoding static color hex codes inside XAML controls.
   - Handle dynamic DPI changes and multi-monitor scaling gracefully.
   - Ensure complete keyboard accessibility and focus navigation.

---

## 2. Standards & Frameworks

- **MVVM Framework**: Use `CommunityToolkit.Mvvm` (Source Generators: `[ObservableProperty]`, `[RelayCommand]`). Do not introduce a second MVVM framework (e.g., Prism, ReactiveUI, MVVMLight).
- **Naming Conventions**:
  - Views: `[Feature]View.xaml` or `[Feature]Window.xaml`
  - ViewModels: `[Feature]ViewModel.cs`
  - Commands: `[Action]Command`
- **Window Isolation**: The Presentation Output Window must never accidentally render the Control Dashboard interface.

---

## 3. Skill Activation Conditions

Activate this skill when:
- Creating or editing XAML views, user controls, data templates, or styles.
- Creating or modifying ViewModels and UI state bindings.
- Configuring window handles (`AppWindow`, `OverlappedPresenter`, window positioning).
- Handling UI thread dispatching, DPI changes, or theme transitions.
- Styling components with WinUI 3 brush resources.

---

## 4. WinUI 3 Quality Checklist

Before finalizing any UI change, verify:
- [ ] No native graphics or capture code inside Views or ViewModels.
- [ ] No `.Result` or `.Wait()` calls on Tasks.
- [ ] Background thread updates to UI state use `DispatcherQueue.TryEnqueue`.
- [ ] Light and Dark theme compatibility verified.
- [ ] Minimum window size constraints are respected.
- [ ] Keyboard navigation and accessibility shortcuts function properly.
