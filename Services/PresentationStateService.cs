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

    public void SetActiveSource(CaptureSource? source)
    {
        lock (_lock)
        {
            ActiveSource = source;
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

            _selectedSources.Remove(existing);
            if (ActiveSource?.Id == sourceId)
            {
                ActiveSource = null;
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
