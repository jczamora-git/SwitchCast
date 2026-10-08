namespace SwitchCast.Models;

/// <summary>
/// Defines how source switching commands (Dock Next/Prev, Direct Selection, Global Hotkeys) operate.
/// </summary>
public enum PresenterSwitchMode
{
    /// <summary>
    /// Switches the live presentation output without changing the user's foreground application focus.
    /// </summary>
    LiveOnly = 0,

    /// <summary>
    /// Requests foreground window activation of the selected application AND switches the live presentation output.
    /// </summary>
    ActiveAndLive = 1,

    /// <summary>
    /// Requests foreground window activation of the selected application WITHOUT altering the audience-facing presentation output.
    /// </summary>
    ActiveOnly = 2
}
