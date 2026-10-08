using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SwitchCast.Models;
using SwitchCast.Services;

namespace SwitchCast.ViewModels;

/// <summary>
/// ViewModel managing presentation source discovery, search filtering, category tabs, and selection state.
/// </summary>
public partial class SourcesViewModel : ObservableObject
{
    private readonly IPresentationStateService _presentationStateService;
    private readonly IWindowDiscoveryService _windowDiscoveryService;
    private readonly IMonitorDiscoveryService _monitorDiscoveryService;
    private readonly IWindowIconService? _windowIconService;

    private readonly List<SelectableSourceItem> _allWindows = [];
    private readonly List<SelectableSourceItem> _allDisplays = [];

    [ObservableProperty]
    private int _selectedCategoryIndex = 0; // 0 = Windows, 1 = Displays

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

    public SourcesViewModel(
        IPresentationStateService presentationStateService,
        IWindowDiscoveryService windowDiscoveryService,
        IMonitorDiscoveryService monitorDiscoveryService,
        IWindowIconService? windowIconService = null)
    {
        _presentationStateService = presentationStateService ?? throw new ArgumentNullException(nameof(presentationStateService));
        _windowDiscoveryService = windowDiscoveryService ?? throw new ArgumentNullException(nameof(windowDiscoveryService));
        _monitorDiscoveryService = monitorDiscoveryService ?? throw new ArgumentNullException(nameof(monitorDiscoveryService));
        _windowIconService = windowIconService;

        DisplayedSources = [];
        _presentationStateService.PropertyChanged += OnPresentationStatePropertyChanged;
    }

    public ObservableCollection<SelectableSourceItem> DisplayedSources { get; }

    public int SelectedSourceCount => _presentationStateService.SelectedSourceCount;

    public bool HasSelectedSources => SelectedSourceCount > 0;

    public bool HasDiscoveredSources => DisplayedSources.Count > 0;

    public bool IsEmpty => !IsRefreshing && DisplayedSources.Count == 0;

    public string EmptyStateTitle => SelectedCategoryIndex == 0
        ? "No Application Windows Found"
        : "No Displays Detected";

    public string EmptyStateSubtitle => string.IsNullOrWhiteSpace(SearchQuery)
        ? (SelectedCategoryIndex == 0
            ? "No running desktop application windows are currently visible. Launch an application and click Refresh."
            : "No display monitors detected on this system. Click Refresh to scan again.")
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

            var activeIds = new HashSet<string>(discoveredWindows.Select(w => w.Id).Concat(discoveredMonitors.Select(m => m.Id)));
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

            WindowCategoryHeader = $"Application Windows ({_allWindows.Count})";
            DisplayCategoryHeader = $"Displays & Monitors ({_allDisplays.Count})";

            ApplyFilter();

            // Asynchronously resolve icons without blocking UI responsiveness
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
                        item.IconSource = icon;
                        item.HasIconSource = true;
                    }
                }
                catch
                {
                    // Fallback glyph remains visible on any error
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
        ApplyFilter();
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        DisplayedSources.Clear();

        var sourceList = SelectedCategoryIndex == 0 ? _allWindows : _allDisplays;
        var query = SearchQuery?.Trim() ?? string.Empty;

        var filtered = string.IsNullOrEmpty(query)
            ? sourceList
            : sourceList.Where(item =>
                item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (item.Source is WindowSource w && w.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase)));

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

            // Sync item selection states
            foreach (var item in DisplayedSources)
            {
                var isSelected = _presentationStateService.IsSourceSelected(item.Id);
                if (item.IsSelected != isSelected)
                {
                    item.IsSelected = isSelected;
                }
            }
        }
    }
}
