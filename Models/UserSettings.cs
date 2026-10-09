namespace SwitchCast.Models;

/// <summary>
/// Serializable local user settings and preferences.
/// </summary>
public class UserSettings
{
    /// <summary>
    /// Preferred application UI theme.
    /// </summary>
    public ApplicationThemeOption Theme { get; set; } = ApplicationThemeOption.System;

    /// <summary>
    /// Last saved window width in DIPs.
    /// </summary>
    public double WindowWidth { get; set; } = 1000;

    /// <summary>
    /// Last saved window height in DIPs.
    /// </summary>
    public double WindowHeight { get; set; } = 700;

    /// <summary>
    /// Last saved window position X in screen physical pixels.
    /// </summary>
    public int? WindowPositionX { get; set; } = null;

    /// <summary>
    /// Last saved window position Y in screen physical pixels.
    /// </summary>
    public int? WindowPositionY { get; set; } = null;

    /// <summary>
    /// Whether to restore window dimensions on startup.
    /// </summary>
    public bool RememberWindowDimensions { get; set; } = true;

    /// <summary>
    /// Whether to restore window position on startup.
    /// </summary>
    public bool RememberWindowPosition { get; set; } = false;

    /// <summary>
    /// Whether system-wide global hotkeys are enabled.
    /// </summary>
    public bool EnableGlobalHotkeys { get; set; } = true;

    /// <summary>
    /// Whether to automatically open the floating presenter dock when presentation starts.
    /// </summary>
    public bool AutoOpenPresenterDock { get; set; } = true;

    /// <summary>
    /// Whether the floating presenter dock stays always-on-top.
    /// </summary>
    public bool DockAlwaysOnTop { get; set; } = true;

    /// <summary>
    /// Whether the floating presenter dock starts in compact single-row mode.
    /// </summary>
    public bool StartDockInCompactMode { get; set; } = false;

    /// <summary>
    /// Active source switching mode for presenter dock and global hotkeys.
    /// </summary>
    public PresenterSwitchMode SwitchMode { get; set; } = PresenterSwitchMode.LiveOnly;

    /// <summary>
    /// Default normalized media audio playback volume (0.0 to 1.0).
    /// </summary>
    public double MediaVolume { get; set; } = 1.0;

    /// <summary>
    /// Whether media audio playback is muted by default.
    /// </summary>
    public bool IsMediaMuted { get; set; } = false;

    /// <summary>
    /// Configured global hotkey bindings.
    /// </summary>
    public List<HotkeyBinding> HotkeyBindings { get; set; } = GetDefaultHotkeys();

    /// <summary>
    /// Persisted list of imported local media file paths.
    /// </summary>
    public List<string> ImportedMediaPaths { get; set; } = [];

    /// <summary>
    /// Generates the standard default hotkey bindings.
    /// </summary>
    public static List<HotkeyBinding> GetDefaultHotkeys() => new()
    {
        new HotkeyBinding
        {
            Action = HotkeyAction.NextSource,
            Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
            VirtualKey = 0x27, // VK_RIGHT
            Name = "Next Presentation Source",
            Description = "Switches presentation to the next queued source."
        },
        new HotkeyBinding
        {
            Action = HotkeyAction.PreviousSource,
            Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
            VirtualKey = 0x25, // VK_LEFT
            Name = "Previous Presentation Source",
            Description = "Switches presentation to the previous queued source."
        },
        new HotkeyBinding
        {
            Action = HotkeyAction.TogglePause,
            Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
            VirtualKey = 0x50, // 'P'
            Name = "Toggle Pause / Freeze Frame",
            Description = "Freezes the current frame or resumes live stream."
        },
        new HotkeyBinding
        {
            Action = HotkeyAction.ToggleBlackout,
            Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
            VirtualKey = 0x42, // 'B'
            Name = "Toggle Privacy Blackout",
            Description = "Instantly blacks out presentation output for privacy."
        },
        new HotkeyBinding
        {
            Action = HotkeyAction.StopPresentation,
            Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
            VirtualKey = 0x53, // 'S'
            Name = "Stop Presenting",
            Description = "Stops live presentation output."
        },
        new HotkeyBinding
        {
            Action = HotkeyAction.TogglePresenterDock,
            Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
            VirtualKey = 0x44, // 'D'
            Name = "Show / Hide Presenter Dock",
            Description = "Toggles visibility of the compact floating dock."
        },
        new HotkeyBinding
        {
            Action = HotkeyAction.ShowDashboard,
            Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
            VirtualKey = 0x4D, // 'M'
            Name = "Show Control Dashboard",
            Description = "Brings the main SwitchCast dashboard to the foreground."
        },
        new HotkeyBinding
        {
            Action = HotkeyAction.SelectSource1,
            Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
            VirtualKey = 0x31, // '1'
            Name = "Switch to Queued Source 1",
            Description = "Directly switches to the first queued source."
        },
        new HotkeyBinding
        {
            Action = HotkeyAction.SelectSource2,
            Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
            VirtualKey = 0x32, // '2'
            Name = "Switch to Queued Source 2",
            Description = "Directly switches to the second queued source."
        },
        new HotkeyBinding
        {
            Action = HotkeyAction.SelectSource3,
            Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
            VirtualKey = 0x33, // '3'
            Name = "Switch to Queued Source 3",
            Description = "Directly switches to the third queued source."
        }
    };
}
