namespace SwitchCast.Services;

/// <summary>
/// Authoritative service governing application window hierarchy, exit confirmation, and coordinated multi-window shutdown.
/// </summary>
public interface IApplicationLifecycleService
{
    /// <summary>
    /// Gets whether application shutdown has been confirmed and authorized by the user.
    /// </summary>
    bool IsShutdownApproved { get; }

    /// <summary>
    /// Gets whether the application is currently executing the safe shutdown sequence.
    /// </summary>
    bool IsShuttingDown { get; }

    /// <summary>
    /// Gets or sets whether an exit confirmation dialog is currently active.
    /// </summary>
    bool IsExitConfirmationOpen { get; set; }

    /// <summary>
    /// Authorizes the application to proceed with final window destruction and shutdown.
    /// </summary>
    void ApproveShutdown();

    /// <summary>
    /// Executes the coordinated, safe shutdown sequence across presentation, capture, windows, hotkeys, and settings.
    /// </summary>
    Task ExecuteShutdownAsync();
}
