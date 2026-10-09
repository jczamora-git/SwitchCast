using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SwitchCast.Models;

namespace SwitchCast.Services;

/// <summary>
/// Implementation of centralized presentation state management.
/// </summary>
public partial class PresentationStateService : ObservableObject, IPresentationStateService
{
    private readonly ObservableCollection<CaptureSource> _selectedSources = [];
    private readonly object _lock = new();

    [ObservableProperty]
    private PresentationStatus _status = PresentationStatus.Idle;

    [ObservableProperty]
    private PresenterSwitchMode _switchMode = PresenterSwitchMode.LiveOnly;

    [ObservableProperty]
    private CaptureSource? _selectedSource;

    [ObservableProperty]
    private CaptureSource? _foregroundSource;

    [ObservableProperty]
    private CaptureSource? _activeSource;

    public PresentationStateService()
    {
        SelectedSources = new ReadOnlyObservableCollection<CaptureSource>(_selectedSources);
    }

    public IReadOnlyList<CaptureSource> SelectedSources { get; }

    public int SelectedSourceCount => _selectedSources.Count;

    public bool IsSourceSelected(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId))
        {
            return false;
        }

        lock (_lock)
        {
            return _selectedSources.Any(s => s.Id == sourceId);
        }
    }

    public void SetStatus(PresentationStatus status)
    {
        lock (_lock)
        {
            Status = status;
        }
    }

    public void SetSwitchMode(PresenterSwitchMode mode)
    {
        lock (_lock)
        {
            SwitchMode = mode;
        }
    }

    public void SetSelectedSource(CaptureSource? source)
    {
        lock (_lock)
        {
            SelectedSource = source;
        }
    }

    public void SetForegroundSource(CaptureSource? source)
    {
        lock (_lock)
        {
            ForegroundSource = source;
        }
    }

    public void SetActiveSource(CaptureSource? source)
    {
        lock (_lock)
        {
            ActiveSource = source;
            if (source is not null && SelectedSource is null)
            {
                SelectedSource = source;
            }
        }
    }

    public bool AddSelectedSource(CaptureSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        lock (_lock)
        {
            if (_selectedSources.Any(s => s.Id == source.Id))
            {
                return false;
            }

            _selectedSources.Add(source);
            OnPropertyChanged(nameof(SelectedSourceCount));
            OnPropertyChanged(nameof(SelectedSources));
            return true;
        }
    }

    public bool RemoveSelectedSource(string sourceId)
    {
        ArgumentNullException.ThrowIfNull(sourceId);

        lock (_lock)
        {
            var existing = _selectedSources.FirstOrDefault(s => s.Id == sourceId);
            if (existing is null)
            {
                return false;
            }

            int existingIndex = _selectedSources.IndexOf(existing);
            _selectedSources.Remove(existing);

            // Per Task Section 17:
            // "Removing a source that IS currently On Air:
            //  Do not unexpectedly stop or switch the presentation without explicit user intent.
            //  Allow the current On Air content to remain visible until the presenter explicitly switches or stops."
            // We intentionally do NOT clear ActiveSource here to preserve On-Air presentation continuity.

            // Per Task Section 18:
            // The selected cursor moves deterministically to the next available queued source,
            // or to the previous item when removing the final entry.
            // If the queue becomes empty, clear the queued-source selection cursor.
            if (SelectedSource?.Id == sourceId)
            {
                if (_selectedSources.Count == 0)
                {
                    SelectedSource = null;
                }
                else
                {
                    int targetIndex = existingIndex < _selectedSources.Count ? existingIndex : existingIndex - 1;
                    SelectedSource = _selectedSources[targetIndex];
                }
            }

            if (ForegroundSource?.Id == sourceId)
            {
                ForegroundSource = null;
            }

            OnPropertyChanged(nameof(SelectedSourceCount));
            OnPropertyChanged(nameof(SelectedSources));
            return true;
        }
    }

    public bool ToggleSourceSelection(CaptureSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        lock (_lock)
        {
            if (IsSourceSelected(source.Id))
            {
                RemoveSelectedSource(source.Id);
                return false;
            }
            else
            {
                AddSelectedSource(source);
                return true;
            }
        }
    }

    public void ClearSelectedSources()
    {
        lock (_lock)
        {
            _selectedSources.Clear();
            ActiveSource = null;
            SelectedSource = null;
            ForegroundSource = null;
            OnPropertyChanged(nameof(SelectedSourceCount));
            OnPropertyChanged(nameof(SelectedSources));
        }
    }

    public void ReconcileAvailability(IReadOnlySet<string> activeDiscoveredIds)
    {
        ArgumentNullException.ThrowIfNull(activeDiscoveredIds);

        lock (_lock)
        {
            for (var i = 0; i < _selectedSources.Count; i++)
            {
                var current = _selectedSources[i];
                var isNowAvailable = activeDiscoveredIds.Contains(current.Id);

                if (current.IsAvailable != isNowAvailable)
                {
                    _selectedSources[i] = current with { IsAvailable = isNowAvailable };
                }
            }

            OnPropertyChanged(nameof(SelectedSources));
        }
    }
}
