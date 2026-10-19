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
public partial class SourcesViewModel : ObservableObject, IDisposable
{
    private readonly IPresentationStateService _presentationStateService;
    private readonly IWindowDiscoveryService _windowDiscoveryService;
    private readonly IMonitorDiscoveryService _monitorDiscoveryService;
    private readonly IWindowIconService? _windowIconService;
    private readonly IMediaDiscoveryService _mediaDiscoveryService;
    private readonly IMediaPickerService _mediaPickerService;
    private readonly Services.PowerPoint.IPowerPointPresentationService? _powerPointService;
    private readonly Services.Camera.ICameraDiscoveryService? _cameraDiscoveryService;
    private readonly IApplicationSettingsService? _settingsService;
    private Microsoft.UI.Dispatching.DispatcherQueue? _dispatcherQueue;
    private IUIThreadDispatcher? _dispatcher;
    private bool _disposed;

    private readonly List<SelectableSourceItem> _allWindows = [];
    private readonly List<SelectableSourceItem> _allDisplays = [];
    private readonly List<SelectableSourceItem> _allMedia = [];
    private readonly List<SelectableSourceItem> _allPowerPoint = [];
    private readonly List<SelectableSourceItem> _allCameras = [];
    private bool _isSyncingSelection;

    [ObservableProperty]
    private int _selectedCategoryIndex = 1; // 0 = All Sources, 1 = Application Windows, 2 = Displays, 3 = Media Files, 4 = PowerPoint, 5 = Cameras

    [ObservableProperty]
    private SourceCategoryOption? _selectedCategoryOption;

    public ObservableCollection<SourceCategoryOption> CategoryOptions { get; }

    [ObservableProperty]
    private bool _showQueuedOnly;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasImportNotice;

    [ObservableProperty]
    private string _importNoticeMessage = string.Empty;

    private Action? _viewImportedAction;

    [ObservableProperty]
    private string _windowCategoryHeader = "Application Windows (0)";

    [ObservableProperty]
    private string _displayCategoryHeader = "Displays & Monitors (0)";

    [ObservableProperty]
    private string _mediaCategoryHeader = "Media Files (0)";

    [ObservableProperty]
    private string _powerPointCategoryHeader = "PowerPoint (0)";

    [ObservableProperty]
    private string _cameraCategoryHeader = "Cameras (0)";

    public SourcesViewModel(
        IPresentationStateService presentationStateService,
        IWindowDiscoveryService windowDiscoveryService,
        IMonitorDiscoveryService monitorDiscoveryService,
        IWindowIconService? windowIconService = null,
        IMediaDiscoveryService? mediaDiscoveryService = null,
        IMediaPickerService? mediaPickerService = null,
        IApplicationSettingsService? settingsService = null,
        Services.PowerPoint.IPowerPointPresentationService? powerPointService = null,
        Services.Camera.ICameraDiscoveryService? cameraDiscoveryService = null)
    {
        _presentationStateService = presentationStateService ?? throw new ArgumentNullException(nameof(presentationStateService));
        _windowDiscoveryService = windowDiscoveryService ?? throw new ArgumentNullException(nameof(windowDiscoveryService));
        _monitorDiscoveryService = monitorDiscoveryService ?? throw new ArgumentNullException(nameof(monitorDiscoveryService));
        _windowIconService = windowIconService;
        _mediaDiscoveryService = mediaDiscoveryService ?? new MediaDiscoveryService();
        _mediaPickerService = mediaPickerService ?? new Win32MediaPickerService();
        _powerPointService = powerPointService;
        _cameraDiscoveryService = cameraDiscoveryService;
        _settingsService = settingsService;
        _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _dispatcher = UIThreadDispatcherProvider.Resolve(null);

        CategoryOptions =
        [
            new SourceCategoryOption(0, "All Sources", 0),
            new SourceCategoryOption(1, "Application Windows", 0),
            new SourceCategoryOption(2, "Displays & Monitors", 0),
            new SourceCategoryOption(3, "Media Files", 0),
            new SourceCategoryOption(4, "PowerPoint", 0),
            new SourceCategoryOption(5, "Cameras", 0)
        ];
        _selectedCategoryOption = CategoryOptions[1];

        DisplayedSources = [];
        _presentationStateService.PropertyChanged += OnPresentationStatePropertyChanged;
    }

    public void SetDispatcherQueue(Microsoft.UI.Dispatching.DispatcherQueue? dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
        _dispatcher = dispatcherQueue is not null ? new DispatcherQueueAdapter(dispatcherQueue) : null;
    }

    public void SetUIThreadDispatcher(IUIThreadDispatcher? dispatcher)
    {
        _dispatcher = dispatcher;
    }

    private void RunOnUIThread(Action action)
    {
        if (_disposed) return;
        if (_dispatcher is not null)
        {
            if (_dispatcher.HasThreadAccess)
            {
                action();
            }
            else
            {
                _dispatcher.TryEnqueue(action);
            }
            return;
        }
        if (_dispatcherQueue is not null)
        {
            if (_dispatcherQueue.HasThreadAccess)
            {
                action();
            }
            else
            {
                _dispatcherQueue.TryEnqueue(() => action());
            }
        }
        else
        {
            action();
        }
    }

    public ObservableCollection<SelectableSourceItem> DisplayedSources { get; }

    public int SelectedSourceCount => _presentationStateService.SelectedSourceCount;

    public bool HasSelectedSources => SelectedSourceCount > 0;

    public string QueueSummaryText => SelectedSourceCount == 1 ? "1 source queued" : $"{SelectedSourceCount} sources queued";

    public bool HasDiscoveredSources => DisplayedSources.Count > 0;

    public bool IsEmpty => !IsRefreshing && DisplayedSources.Count == 0;

    public bool IsAllSourcesCategorySelected => SelectedCategoryIndex == 0;

    public bool IsWindowsCategorySelected => SelectedCategoryIndex == 1;

    public bool IsDisplaysCategorySelected => SelectedCategoryIndex == 2;

    public bool IsMediaCategorySelected => SelectedCategoryIndex == 3;

    public bool IsPowerPointCategorySelected => SelectedCategoryIndex == 4;

    public bool IsCameraCategorySelected => SelectedCategoryIndex == 5;

    public bool IsSearchQueryNotEmpty => !string.IsNullOrWhiteSpace(SearchQuery);

    public bool HasSearchFeedback => IsSearchQueryNotEmpty && HasDiscoveredSources;

    public string SearchFeedbackText => DisplayedSources.Count == 1 ? "1 result" : $"{DisplayedSources.Count} results";

    public int TotalSourcesCount => _allWindows.Count + _allDisplays.Count + _allMedia.Count + _allPowerPoint.Count + _allCameras.Count;

    public IReadOnlyList<string> CategoryDisplayOptions =>
        CategoryOptions.Select(c => c.DisplayText).ToList();

    public string EmptyStateTitle
    {
        get
        {
            if (IsSearchQueryNotEmpty)
            {
                return "No matching sources";
            }
            if (ShowQueuedOnly)
            {
                return "No queued sources";
            }
            return SelectedCategoryIndex switch
            {
                0 => "No Presentation Sources Found",
                1 => "No matching application windows",
                2 => "No Displays Detected",
                3 => "No Media Files Imported",
                4 => "No PowerPoint files imported",
                _ => "No cameras detected"
            };
        }
    }

    public string EmptyStateSubtitle
    {
        get
        {
            if (IsSearchQueryNotEmpty)
            {
                return "Try another search term or change the category.";
            }
            if (ShowQueuedOnly)
            {
                return "No sources are currently queued in this category. Check the Queue box on any source to include it.";
            }
            return SelectedCategoryIndex switch
            {
                0 => "No presentation sources discovered. Click Refresh or import files.",
                1 => "No running desktop application windows are currently visible. Launch an application and click Refresh.",
                2 => "No display monitors detected on this system. Click Refresh to scan again.",
                3 => "Import local images and videos to present them directly without third-party applications. Click 'Add Media'.",
                4 => "Import PowerPoint decks (.pptx) to present slides directly or launch live slideshows. Click 'Import PowerPoint'.",
                _ => "No webcam or USB video capture devices detected. Connect a camera and click Refresh."
            };
        }
    }

    [RelayCommand]
    public void ViewImportedSource()
    {
        _viewImportedAction?.Invoke();
        _viewImportedAction = null;
        HasImportNotice = false;
    }

    [RelayCommand]
    public void DismissImportNotice()
    {
        HasImportNotice = false;
        _viewImportedAction = null;
    }

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
            var camerasTask = _cameraDiscoveryService is not null
                ? _cameraDiscoveryService.EnumerateCamerasAsync()
                : Task.FromResult<IReadOnlyList<CameraSource>>([]);

            await Task.WhenAll(windowsTask, monitorsTask, camerasTask).ConfigureAwait(true);

            var discoveredWindows = await windowsTask.ConfigureAwait(true);
            var discoveredMonitors = await monitorsTask.ConfigureAwait(true);
            var discoveredCameras = await camerasTask.ConfigureAwait(true);

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

            // Load persisted PowerPoint presentations if not yet loaded
            if (_allPowerPoint.Count == 0 && _settingsService?.CurrentSettings.ImportedPowerPointPaths is { Count: > 0 } pptPaths)
            {
                foreach (var path in pptPaths)
                {
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }

                    PowerPointSource pptSource;
                    if (File.Exists(path))
                    {
                        if (_powerPointService is not null)
                        {
                            try
                            {
                                pptSource = await _powerPointService.ImportPresentationAsync(path).ConfigureAwait(true);
                            }
                            catch
                            {
                                pptSource = new PowerPointSource(path) { IsAvailable = true };
                            }
                        }
                        else
                        {
                            pptSource = new PowerPointSource(path) { IsAvailable = true };
                        }
                    }
                    else
                    {
                        pptSource = new PowerPointSource(path) { IsAvailable = false };
                    }

                    var isSelected = _presentationStateService.IsSourceSelected(pptSource.Id);
                    var item = new SelectableSourceItem(pptSource, isSelected, OnItemSelectionChanged)
                    {
                        IsAvailable = pptSource.IsAvailable
                    };
                    _allPowerPoint.Add(item);
                }
            }
            else
            {
                // Reconcile existing PowerPoint availability without deleting imported library entries
                for (int i = 0; i < _allPowerPoint.Count; i++)
                {
                    var item = _allPowerPoint[i];
                    if (item.Source is PowerPointSource ppt)
                    {
                        item.IsAvailable = File.Exists(ppt.FilePath);
                    }
                }
            }

            var activeIds = new HashSet<string>(
                discoveredWindows.Select(w => w.Id)
                .Concat(discoveredMonitors.Select(m => m.Id))
                .Concat(_allMedia.Where(m => m.IsAvailable).Select(m => m.Id))
                .Concat(_allPowerPoint.Where(p => p.IsAvailable).Select(p => p.Id))
                .Concat(discoveredCameras.Select(c => c.Id)));

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

            _allCameras.Clear();
            foreach (var cam in discoveredCameras)
            {
                var isSelected = _presentationStateService.IsSourceSelected(cam.Id);
                _allCameras.Add(new SelectableSourceItem(cam, isSelected, OnItemSelectionChanged));
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
            SelectableSourceItem? lastAddedItem = null;

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
                    lastAddedItem = item;
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

                // Provide feedback if active category or filters exclude the imported item
                if (lastAddedItem != null && !IsItemVisibleUnderFilter(lastAddedItem, 3))
                {
                    var fileName = Path.GetFileName((lastAddedItem.Source as MediaFileSource)?.FilePath ?? lastAddedItem.Title);
                    ImportNoticeMessage = $"{fileName} imported successfully";
                    _viewImportedAction = () =>
                    {
                        SelectedCategoryIndex = 3;
                        ShowQueuedOnly = false;
                        if (!string.IsNullOrWhiteSpace(SearchQuery) && !MatchesSearch(lastAddedItem, SearchQuery))
                        {
                            SearchQuery = string.Empty;
                        }
                    };
                    HasImportNotice = true;
                }
                else
                {
                    HasImportNotice = false;
                }
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

    [RelayCommand]
    public async Task AddPowerPointAsync()
    {
        try
        {
            var pickedPaths = await _mediaPickerService.PickPowerPointFilesAsync().ConfigureAwait(true);
            if (pickedPaths.Count == 0)
            {
                return;
            }

            var existingPaths = new HashSet<string>(
                _allPowerPoint.Select(p => (p.Source as PowerPointSource)?.FilePath ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);

            bool addedAny = false;
            SelectableSourceItem? lastAddedItem = null;

            foreach (var path in pickedPaths)
            {
                if (string.IsNullOrWhiteSpace(path) || existingPaths.Contains(path) || !File.Exists(path))
                {
                    continue;
                }

                PowerPointSource pptSource;
                if (_powerPointService is not null)
                {
                    pptSource = await _powerPointService.ImportPresentationAsync(path).ConfigureAwait(true);
                }
                else
                {
                    pptSource = new PowerPointSource(path);
                }

                var isSelected = _presentationStateService.IsSourceSelected(pptSource.Id);
                var item = new SelectableSourceItem(pptSource, isSelected, OnItemSelectionChanged);
                _allPowerPoint.Add(item);
                existingPaths.Add(path);
                lastAddedItem = item;
                addedAny = true;
            }

            if (addedAny)
            {
                if (_settingsService is not null)
                {
                    _settingsService.CurrentSettings.ImportedPowerPointPaths = _allPowerPoint
                        .Select(p => (p.Source as PowerPointSource)?.FilePath)
                        .Where(p => !string.IsNullOrEmpty(p))
                        .Select(p => p!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    await _settingsService.SaveSettingsAsync().ConfigureAwait(true);
                }

                UpdateCategoryHeaders();
                ApplyFilter();

                // Provide feedback if active category or filters exclude the imported item
                if (lastAddedItem != null && !IsItemVisibleUnderFilter(lastAddedItem, 4))
                {
                    var fileName = Path.GetFileName((lastAddedItem.Source as PowerPointSource)?.FilePath ?? lastAddedItem.Title);
                    ImportNoticeMessage = $"{fileName} imported successfully";
                    _viewImportedAction = () =>
                    {
                        SelectedCategoryIndex = 4;
                        ShowQueuedOnly = false;
                        if (!string.IsNullOrWhiteSpace(SearchQuery) && !MatchesSearch(lastAddedItem, SearchQuery))
                        {
                            SearchQuery = string.Empty;
                        }
                    };
                    HasImportNotice = true;
                }
                else
                {
                    HasImportNotice = false;
                }
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Failed to import PowerPoint: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task RemovePowerPointAsync(SelectableSourceItem? item)
    {
        if (item is null || item.Source is not PowerPointSource ppt)
        {
            return;
        }

        try
        {
            _allPowerPoint.Remove(item);
            _presentationStateService.RemoveSelectedSource(item.Id);

            if (_settingsService is not null)
            {
                _settingsService.CurrentSettings.ImportedPowerPointPaths = _allPowerPoint
                    .Select(p => (p.Source as PowerPointSource)?.FilePath)
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
            ErrorMessage = $"Failed to remove PowerPoint presentation: {ex.Message}";
        }
    }

    public void RemovePowerPoint(SelectableSourceItem? item)
    {
        _ = RemovePowerPointAsync(item);
    }

    private void UpdateCategoryHeaders()
    {
        CategoryOptions[0].UpdateCount(TotalSourcesCount);
        CategoryOptions[1].UpdateCount(_allWindows.Count);
        CategoryOptions[2].UpdateCount(_allDisplays.Count);
        CategoryOptions[3].UpdateCount(_allMedia.Count);
        CategoryOptions[4].UpdateCount(_allPowerPoint.Count);
        CategoryOptions[5].UpdateCount(_allCameras.Count);

        WindowCategoryHeader = CategoryOptions[1].DisplayText;
        DisplayCategoryHeader = CategoryOptions[2].DisplayText;
        MediaCategoryHeader = CategoryOptions[3].DisplayText;
        PowerPointCategoryHeader = CategoryOptions[4].DisplayText;
        CameraCategoryHeader = CategoryOptions[5].DisplayText;

        OnPropertyChanged(nameof(TotalSourcesCount));
        OnPropertyChanged(nameof(CategoryDisplayOptions));
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
        if (value < 0 || value >= CategoryOptions.Count)
        {
            value = 1;
            SelectedCategoryIndex = 1;
            return;
        }

        if (SelectedCategoryOption != CategoryOptions[value])
        {
            SelectedCategoryOption = CategoryOptions[value];
        }

        OnPropertyChanged(nameof(IsAllSourcesCategorySelected));
        OnPropertyChanged(nameof(IsWindowsCategorySelected));
        OnPropertyChanged(nameof(IsDisplaysCategorySelected));
        OnPropertyChanged(nameof(IsMediaCategorySelected));
        OnPropertyChanged(nameof(IsPowerPointCategorySelected));
        OnPropertyChanged(nameof(IsCameraCategorySelected));
        ApplyFilter();
    }

    partial void OnSelectedCategoryOptionChanged(SourceCategoryOption? value)
    {
        if (value is not null && SelectedCategoryIndex != value.CategoryIndex)
        {
            SelectedCategoryIndex = value.CategoryIndex;
        }
    }

    partial void OnShowQueuedOnlyChanged(bool value)
    {
        ApplyFilter();
    }

    partial void OnSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(IsSearchQueryNotEmpty));
        ApplyFilter();
    }

    private static bool MatchesSearch(SelectableSourceItem item, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        if (item.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (item.Source is WindowSource w)
        {
            if (w.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(w.ProcessPath) && w.ProcessPath.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        }
        else if (item.Source is MediaFileSource m)
        {
            if (m.FilePath.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        }
        else if (item.Source is PowerPointSource p)
        {
            if (p.FilePath.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        }
        else if (item.Source is MonitorSource mon)
        {
            if (mon.DeviceName.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        }
        else if (item.Source is CameraSource cam)
        {
            if (cam.DeviceId.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private bool IsItemVisibleUnderFilter(SelectableSourceItem item, int targetCategoryIndex)
    {
        if (SelectedCategoryIndex != 0 && SelectedCategoryIndex != targetCategoryIndex)
        {
            return false;
        }

        if (ShowQueuedOnly && !_presentationStateService.IsSourceSelected(item.Id))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(SearchQuery) && !MatchesSearch(item, SearchQuery))
        {
            return false;
        }

        return true;
    }

    private void ApplyFilter()
    {
        RunOnUIThread(() =>
        {
            DisplayedSources.Clear();

            IEnumerable<SelectableSourceItem> sourceList = SelectedCategoryIndex switch
            {
                0 => _allWindows.Concat(_allDisplays).Concat(_allMedia).Concat(_allPowerPoint).Concat(_allCameras),
                1 => _allWindows,
                2 => _allDisplays,
                3 => _allMedia,
                4 => _allPowerPoint,
                5 => _allCameras,
                _ => _allWindows
            };

            if (ShowQueuedOnly)
            {
                sourceList = sourceList.Where(item => _presentationStateService.IsSourceSelected(item.Id));
            }

            var query = SearchQuery?.Trim() ?? string.Empty;

            var filtered = string.IsNullOrEmpty(query)
                ? sourceList
                : sourceList.Where(item => MatchesSearch(item, query));

            _isSyncingSelection = true;
            try
            {
                foreach (var item in filtered)
                {
                    // Sync selection state with authoritative state service
                    item.IsSelected = _presentationStateService.IsSourceSelected(item.Id);
                    DisplayedSources.Add(item);
                }
            }
            finally
            {
                _isSyncingSelection = false;
            }

            OnPropertyChanged(nameof(HasDiscoveredSources));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(EmptyStateTitle));
            OnPropertyChanged(nameof(EmptyStateSubtitle));
            OnPropertyChanged(nameof(HasSearchFeedback));
            OnPropertyChanged(nameof(SearchFeedbackText));
            OnPropertyChanged(nameof(QueueSummaryText));
        });
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

        OnPropertyChanged(nameof(QueueSummaryText));

        if (ShowQueuedOnly)
        {
            ApplyFilter();
        }
    }

    private void OnPresentationStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IPresentationStateService.SelectedSourceCount) ||
            e.PropertyName == nameof(IPresentationStateService.SelectedSources))
        {
            RunOnUIThread(() =>
            {
                OnPropertyChanged(nameof(SelectedSourceCount));
                OnPropertyChanged(nameof(HasSelectedSources));
                OnPropertyChanged(nameof(QueueSummaryText));

                _isSyncingSelection = true;
                try
                {
                    // Sync item selection states across all discovered collections so switching tabs shows accurate state
                    foreach (var item in _allWindows.Concat(_allDisplays).Concat(_allMedia).Concat(_allPowerPoint).Concat(_allCameras))
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

                if (ShowQueuedOnly)
                {
                    ApplyFilter();
                }
            });
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _presentationStateService.PropertyChanged -= OnPresentationStatePropertyChanged;
    }
}
