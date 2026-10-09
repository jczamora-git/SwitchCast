using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.Services.Media;

namespace SwitchCast.ViewModels;

/// <summary>
/// ViewModel managing presentation source discovery, search filtering, category tabs (Windows, Displays, Media Files), and selection state.
/// </summary>
public partial class SourcesViewModel : ObservableObject
{
    private readonly IPresentationStateService _presentationStateService;
    private readonly IWindowDiscoveryService _windowDiscoveryService;
    private readonly IMonitorDiscoveryService _monitorDiscoveryService;
    private readonly IWindowIconService? _windowIconService;
    private readonly IMediaDiscoveryService _mediaDiscoveryService;
    private readonly IMediaPickerService _mediaPickerService;
    private readonly IApplicationSettingsService? _settingsService;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue? _dispatcherQueue;

    private readonly List<SelectableSourceItem> _allWindows = [];
    private readonly List<SelectableSourceItem> _allDisplays = [];
    private readonly List<SelectableSourceItem> _allMedia = [];
    private bool _isSyncingSelection;

    [ObservableProperty]
    private int _selectedCategoryIndex = 0; // 0 = Windows, 1 = Displays, 2 = Media Files

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _windowCategoryHeader = "Application Windows (0)";

    [ObservableProperty]
    private string _displayCategoryHeader = "Displays & Monitors (0)";

    [ObservableProperty]
    private string _mediaCategoryHeader = "Media Files (0)";

    public SourcesViewModel(
        IPresentationStateService presentationStateService,
        IWindowDiscoveryService windowDiscoveryService,
        IMonitorDiscoveryService monitorDiscoveryService,
        IWindowIconService? windowIconService = null,
        IMediaDiscoveryService? mediaDiscoveryService = null,
        IMediaPickerService? mediaPickerService = null,
        IApplicationSettingsService? settingsService = null)
    {
        _presentationStateService = presentationStateService ?? throw new ArgumentNullException(nameof(presentationStateService));
        _windowDiscoveryService = windowDiscoveryService ?? throw new ArgumentNullException(nameof(windowDiscoveryService));
        _monitorDiscoveryService = monitorDiscoveryService ?? throw new ArgumentNullException(nameof(monitorDiscoveryService));
        _windowIconService = windowIconService;
        _mediaDiscoveryService = mediaDiscoveryService ?? new MediaDiscoveryService();
        _mediaPickerService = mediaPickerService ?? new Win32MediaPickerService();
        _settingsService = settingsService;
        _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        DisplayedSources = [];
        _presentationStateService.PropertyChanged += OnPresentationStatePropertyChanged;
    }

    public ObservableCollection<SelectableSourceItem> DisplayedSources { get; }

    public int SelectedSourceCount => _presentationStateService.SelectedSourceCount;

    public bool HasSelectedSources => SelectedSourceCount > 0;

    public bool HasDiscoveredSources => DisplayedSources.Count > 0;

    public bool IsEmpty => !IsRefreshing && DisplayedSources.Count == 0;

    public bool IsMediaCategorySelected => SelectedCategoryIndex == 2;

    public string EmptyStateTitle => SelectedCategoryIndex switch
    {
        0 => "No Application Windows Found",
        1 => "No Displays Detected",
        _ => "No Media Files Imported"
    };

    public string EmptyStateSubtitle => string.IsNullOrWhiteSpace(SearchQuery)
        ? (SelectedCategoryIndex switch
        {
            0 => "No running desktop application windows are currently visible. Launch an application and click Refresh.",
            1 => "No display monitors detected on this system. Click Refresh to scan again.",
            _ => "Import local images and videos to present them directly without third-party applications. Click 'Add Media'."
        })
        : $"No presentation sources match '{SearchQuery}'.";

    [RelayCommand]
    public async Task RefreshSourcesAsync()
    {
        if (IsRefreshing)
        {
            return;
        }

        IsRefreshing = true;
        HasError = false;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(IsEmpty));

        try
        {
            var windowsTask = _windowDiscoveryService.EnumerateWindowsAsync();
            var monitorsTask = _monitorDiscoveryService.EnumerateMonitorsAsync();

            await Task.WhenAll(windowsTask, monitorsTask).ConfigureAwait(true);

            var discoveredWindows = await windowsTask.ConfigureAwait(true);
            var discoveredMonitors = await monitorsTask.ConfigureAwait(true);

            // Load persisted media files if not yet loaded
            if (_allMedia.Count == 0 && _settingsService?.CurrentSettings.ImportedMediaPaths is { Count: > 0 } paths)
            {
                var persistedMedia = await _mediaDiscoveryService.LoadPersistedMediaAsync(paths).ConfigureAwait(true);
                _allMedia.Clear();
                foreach (var media in persistedMedia)
                {
                    var isSelected = _presentationStateService.IsSourceSelected(media.Id);
                    _allMedia.Add(new SelectableSourceItem(media, isSelected, OnItemSelectionChanged));
                }
            }
            else
            {
                // Reconcile existing media availability
                for (int i = 0; i < _allMedia.Count; i++)
                {
                    var item = _allMedia[i];
                    if (item.Source is MediaFileSource mfs)
                    {
                        bool exists = File.Exists(mfs.FilePath);
                        item.IsAvailable = exists;
                    }
                }
            }

            var activeIds = new HashSet<string>(
                discoveredWindows.Select(w => w.Id)
                .Concat(discoveredMonitors.Select(m => m.Id))
                .Concat(_allMedia.Where(m => m.IsAvailable).Select(m => m.Id)));

            _presentationStateService.ReconcileAvailability(activeIds);

            _allWindows.Clear();
            foreach (var win in discoveredWindows)
            {
                var isSelected = _presentationStateService.IsSourceSelected(win.Id);
                _allWindows.Add(new SelectableSourceItem(win, isSelected, OnItemSelectionChanged));
            }

            _allDisplays.Clear();
            foreach (var mon in discoveredMonitors)
            {
                var isSelected = _presentationStateService.IsSourceSelected(mon.Id);
                _allDisplays.Add(new SelectableSourceItem(mon, isSelected, OnItemSelectionChanged));
            }

            UpdateCategoryHeaders();
            ApplyFilter();

            // Asynchronously resolve icons for windows without blocking UI responsiveness
            _ = LoadIconsAsync(_allWindows);
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Discovery error: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
            OnPropertyChanged(nameof(HasDiscoveredSources));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(EmptyStateTitle));
            OnPropertyChanged(nameof(EmptyStateSubtitle));
        }
    }

    [RelayCommand]
    public async Task AddMediaAsync()
    {
        try
        {
            var pickedPaths = await _mediaPickerService.PickMediaFilesAsync().ConfigureAwait(true);
            if (pickedPaths.Count == 0)
            {
                return;
            }

            var existingPaths = new HashSet<string>(
                _allMedia.Select(m => (m.Source as MediaFileSource)?.FilePath ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);

            bool addedAny = false;
            foreach (var path in pickedPaths)
            {
                if (string.IsNullOrWhiteSpace(path) || existingPaths.Contains(path))
                {
                    continue;
                }

                var mediaSource = await _mediaDiscoveryService.CreateMediaSourceFromFileAsync(path).ConfigureAwait(true);
                if (mediaSource is not null)
                {
                    var isSelected = _presentationStateService.IsSourceSelected(mediaSource.Id);
                    var item = new SelectableSourceItem(mediaSource, isSelected, OnItemSelectionChanged);
                    _allMedia.Add(item);
                    existingPaths.Add(path);
                    addedAny = true;
                }
            }

            if (addedAny)
            {
                if (_settingsService is not null)
                {
                    _settingsService.CurrentSettings.ImportedMediaPaths = _allMedia
                        .Select(m => (m.Source as MediaFileSource)?.FilePath)
                        .Where(p => !string.IsNullOrEmpty(p))
                        .Select(p => p!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    await _settingsService.SaveSettingsAsync().ConfigureAwait(true);
                }

                UpdateCategoryHeaders();
                ApplyFilter();
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Failed to add media: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task RemoveMediaAsync(SelectableSourceItem? item)
    {
        if (item is null || item.Source is not MediaFileSource mediaSource)
        {
            return;
        }

        try
        {
            _allMedia.Remove(item);
            _presentationStateService.RemoveSelectedSource(item.Id);

            if (_settingsService is not null)
            {
                _settingsService.CurrentSettings.ImportedMediaPaths = _allMedia
                    .Select(m => (m.Source as MediaFileSource)?.FilePath)
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Select(p => p!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                await _settingsService.SaveSettingsAsync().ConfigureAwait(true);
            }

            UpdateCategoryHeaders();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Failed to remove media: {ex.Message}";
        }
    }

    private void UpdateCategoryHeaders()
    {
        WindowCategoryHeader = $"Application Windows ({_allWindows.Count})";
        DisplayCategoryHeader = $"Displays & Monitors ({_allDisplays.Count})";
        MediaCategoryHeader = $"Media Files ({_allMedia.Count})";
    }

    private async Task LoadIconsAsync(IEnumerable<SelectableSourceItem> items)
    {
        if (_windowIconService is null)
        {
            return;
        }

        var windowItems = items.Where(i => i.Source is WindowSource).ToList();
        if (windowItems.Count == 0)
        {
            return;
        }

        var dispatcher = _dispatcherQueue ?? Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        using var semaphore = new SemaphoreSlim(8);
        var tasks = windowItems.Select(async item =>
        {
            if (item.Source is WindowSource winSource)
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    var icon = await _windowIconService.GetIconForSourceAsync(winSource).ConfigureAwait(false);
                    if (icon is not null)
                    {
                        if (dispatcher is not null && !dispatcher.HasThreadAccess)
                        {
                            dispatcher.TryEnqueue(() =>
                            {
                                item.IconSource = icon;
                            });
                        }
                        else
                        {
                            item.IconSource = icon;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SourcesViewModel] Error loading icon for {winSource.Title}: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    partial void OnSelectedCategoryIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsMediaCategorySelected));
        ApplyFilter();
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        DisplayedSources.Clear();

        var sourceList = SelectedCategoryIndex switch
        {
            0 => _allWindows,
            1 => _allDisplays,
            _ => _allMedia
        };

        var query = SearchQuery?.Trim() ?? string.Empty;

        var filtered = string.IsNullOrEmpty(query)
            ? sourceList
            : sourceList.Where(item =>
                item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (item.Source is WindowSource w && w.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (item.Source is MediaFileSource m && m.FilePath.Contains(query, StringComparison.OrdinalIgnoreCase)));

        foreach (var item in filtered)
        {
            // Sync selection state with authoritative state service
            item.IsSelected = _presentationStateService.IsSourceSelected(item.Id);
            DisplayedSources.Add(item);
        }

        OnPropertyChanged(nameof(HasDiscoveredSources));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateSubtitle));
    }

    private void OnItemSelectionChanged(SelectableSourceItem item, bool isSelected)
    {
        if (_isSyncingSelection)
        {
            return;
        }

        if (isSelected)
        {
            _presentationStateService.AddSelectedSource(item.Source);
        }
        else
        {
            _presentationStateService.RemoveSelectedSource(item.Id);
        }
    }

    private void OnPresentationStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IPresentationStateService.SelectedSourceCount) ||
            e.PropertyName == nameof(IPresentationStateService.SelectedSources))
        {
            OnPropertyChanged(nameof(SelectedSourceCount));
            OnPropertyChanged(nameof(HasSelectedSources));

            _isSyncingSelection = true;
            try
            {
                // Sync item selection states across all discovered collections so switching tabs shows accurate state
                foreach (var item in _allWindows.Concat(_allDisplays).Concat(_allMedia))
                {
                    var isSelected = _presentationStateService.IsSourceSelected(item.Id);
                    if (item.IsSelected != isSelected)
                    {
                        item.IsSelected = isSelected;
                    }
                }
            }
            finally
            {
                _isSyncingSelection = false;
            }
        }
    }
}
