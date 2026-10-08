using System.ComponentModel;
using SwitchCast.Models;

namespace SwitchCast.Services;

/// <summary>
/// Authoritative centralized service managing presentation session state, active source, and selected source queue.
/// </summary>
public interface IPresentationStateService : INotifyPropertyChanged
{
    /// <summary>
    /// Current operational status of the presentation.
    /// </summary>
    PresentationStatus Status { get; }

    /// <summary>
    /// Active source switching mode for presenter dock and global hotkeys.
    /// </summary>
    PresenterSwitchMode SwitchMode { get; }

    /// <summary>
    /// Currently selected source in the navigation queue cursor.
    /// </summary>
    CaptureSource? SelectedSource { get; }

    /// <summary>
    /// Last confirmed foreground activated source, or null if none.
    /// </summary>
    CaptureSource? ForegroundSource { get; }

    /// <summary>
    /// Currently active presentation source being captured / displayed (on-air), or null if none.
    /// </summary>
    CaptureSource? ActiveSource { get; }

    /// <summary>
    /// List of user-selected sources queued for presentation.
    /// </summary>
    IReadOnlyList<CaptureSource> SelectedSources { get; }

    /// <summary>
    /// Total count of selected sources.
    /// </summary>
    int SelectedSourceCount { get; }

    /// <summary>
    /// Returns true if a source with the specified identifier is currently selected.
    /// </summary>
    bool IsSourceSelected(string sourceId);

    /// <summary>
    /// Updates the current presentation status.
    /// </summary>
    void SetStatus(PresentationStatus status);

    /// <summary>
    /// Sets the active presentation switching mode.
    /// </summary>
    void SetSwitchMode(PresenterSwitchMode mode);

    /// <summary>
    /// Sets the selected source cursor position.
    /// </summary>
    void SetSelectedSource(CaptureSource? source);

    /// <summary>
    /// Sets the confirmed foreground application window source.
    /// </summary>
    void SetForegroundSource(CaptureSource? source);

    /// <summary>
    /// Sets the active presentation source.
    /// </summary>
    void SetActiveSource(CaptureSource? source);

    /// <summary>
    /// Adds a source to the presenter's active queue.
    /// </summary>
    bool AddSelectedSource(CaptureSource source);

    /// <summary>
    /// Removes a source from the presenter's active queue by identifier.
    /// </summary>
    bool RemoveSelectedSource(string sourceId);

    /// <summary>
    /// Toggles the selection state of a source.
    /// </summary>
    bool ToggleSourceSelection(CaptureSource source);

    /// <summary>
    /// Clears all sources from the presenter's queue.
    /// </summary>
    void ClearSelectedSources();

    /// <summary>
    /// Reconciles the availability status of all currently selected sources against newly discovered IDs.
    /// </summary>
    void ReconcileAvailability(IReadOnlySet<string> activeDiscoveredIds);
}
