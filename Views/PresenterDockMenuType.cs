namespace SwitchCast.Views;

/// <summary>
/// Identifies the type of dropdown menu hosted in the external Presenter Dock Menu window.
/// </summary>
public enum PresenterDockMenuType
{
    /// <summary>
    /// Dropdown listing all currently queued sources for immediate selection.
    /// </summary>
    QueuedSources,

    /// <summary>
    /// Dropdown selecting the active presenter switching mode (A+L, A, L).
    /// </summary>
    SwitchMode,

    /// <summary>
    /// Menu providing secondary actions (Stop Presenting, Dashboard, Output Window).
    /// </summary>
    MoreOptions
}
