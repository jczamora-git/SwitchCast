using SwitchCast.Models;

namespace SwitchCast.Services;

/// <summary>
/// Service managing the registration, unregistration, and dispatching of Windows global keyboard hotkeys.
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>
    /// Gets whether global hotkeys are currently active and listening.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Current registered or configured hotkey bindings.
    /// </summary>
    IReadOnlyList<HotkeyBinding> CurrentBindings { get; }

    /// <summary>
    /// Event fired when a registered global hotkey combination is triggered by the presenter.
    /// </summary>
    event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;

    /// <summary>
    /// Initializes the underlying Win32 message listener and registers enabled hotkeys.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Registers all enabled hotkey bindings from user settings.
    /// </summary>
    bool RegisterAllHotkeys();

    /// <summary>
    /// Unregisters all currently registered global hotkeys.
    /// </summary>
    void UnregisterAllHotkeys();

    /// <summary>
    /// Registers an individual hotkey binding.
    /// </summary>
    bool RegisterHotkey(HotkeyBinding binding);

    /// <summary>
    /// Unregisters an individual hotkey by action.
    /// </summary>
    void UnregisterHotkey(HotkeyAction action);

    /// <summary>
    /// Enables or disables global hotkey listening at runtime.
    /// </summary>
    void SetEnabled(bool isEnabled);

    /// <summary>
    /// Reloads bindings from user settings and updates registration.
    /// </summary>
    void ReloadSettings();
}
