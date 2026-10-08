using System.Text.Json.Serialization;

namespace SwitchCast.Models;

/// <summary>
/// Supported actions triggered via global keyboard hotkeys.
/// </summary>
public enum HotkeyAction
{
    NextSource,
    PreviousSource,
    TogglePause,
    ToggleBlackout,
    StopPresentation,
    TogglePresenterDock,
    ShowDashboard,
    SelectSource1,
    SelectSource2,
    SelectSource3,
    SelectSource4,
    SelectSource5
}

/// <summary>
/// Bitwise flags representing modifier keys for Win32 RegisterHotKey.
/// </summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0x0000,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008,
    NoRepeat = 0x4000
}

/// <summary>
/// Represents a user-configurable hotkey combination bound to an action.
/// </summary>
public class HotkeyBinding
{
    public HotkeyAction Action { get; set; }

    public HotkeyModifiers Modifiers { get; set; } = HotkeyModifiers.Control | HotkeyModifiers.Shift;

    public uint VirtualKey { get; set; }

    public bool IsEnabled { get; set; } = true;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsRegistered { get; set; }

    [JsonIgnore]
    public string? RegistrationError { get; set; }

    [JsonIgnore]
    public string DisplayString => FormatDisplayString();

    private string FormatDisplayString()
    {
        var parts = new List<string>();

        if (Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(FormatKeyName(VirtualKey));

        return string.Join(" + ", parts);
    }

    private static string FormatKeyName(uint vk) => vk switch
    {
        0x25 => "Left Arrow",
        0x26 => "Up Arrow",
        0x27 => "Right Arrow",
        0x28 => "Down Arrow",
        0x20 => "Space",
        0x0D => "Enter",
        0x1B => "Escape",
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        >= 0x70 and <= 0x7B => $"F{vk - 0x70 + 1}",
        _ => $"0x{vk:X2}"
    };
}

/// <summary>
/// Event arguments delivered when a registered global hotkey is pressed.
/// </summary>
public class HotkeyTriggeredEventArgs : EventArgs
{
    public HotkeyTriggeredEventArgs(HotkeyAction action, HotkeyBinding binding)
    {
        Action = action;
        Binding = binding;
    }

    public HotkeyAction Action { get; }

    public HotkeyBinding Binding { get; }
}
