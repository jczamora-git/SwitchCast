using System.Diagnostics;
using SwitchCast.Services.Capture;

namespace SwitchCast.Services;

/// <summary>
/// Authoritative implementation of application lifecycle and multi-window shutdown orchestration.
/// </summary>
public sealed class ApplicationLifecycleService : IApplicationLifecycleService
{
    private readonly IPresentationCoordinator _presentationCoordinator;
    private readonly ICaptureCoordinator _captureCoordinator;
    private readonly IPresentationWindowService _presentationWindowService;
    private readonly IPresenterDockService _presenterDockService;
    private readonly IHotkeyService _hotkeyService;
    private readonly IApplicationSettingsService _settingsService;

    private int _isShuttingDown;
    private int _isShutdownApproved;
    private int _isExitConfirmationOpen;

    public ApplicationLifecycleService(
        IPresentationCoordinator presentationCoordinator,
        ICaptureCoordinator captureCoordinator,
        IPresentationWindowService presentationWindowService,
        IPresenterDockService presenterDockService,
        IHotkeyService hotkeyService,
        IApplicationSettingsService settingsService)
    {
        _presentationCoordinator = presentationCoordinator ?? throw new ArgumentNullException(nameof(presentationCoordinator));
        _captureCoordinator = captureCoordinator ?? throw new ArgumentNullException(nameof(captureCoordinator));
        _presentationWindowService = presentationWindowService ?? throw new ArgumentNullException(nameof(presentationWindowService));
        _presenterDockService = presenterDockService ?? throw new ArgumentNullException(nameof(presenterDockService));
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    public bool IsShutdownApproved => Volatile.Read(ref _isShutdownApproved) == 1;

    public bool IsShuttingDown => Volatile.Read(ref _isShuttingDown) == 1;

    public bool IsExitConfirmationOpen
    {
        get => Volatile.Read(ref _isExitConfirmationOpen) == 1;
        set => Interlocked.Exchange(ref _isExitConfirmationOpen, value ? 1 : 0);
    }

    public void ApproveShutdown()
    {
        Interlocked.Exchange(ref _isShutdownApproved, 1);
    }

    public async Task ExecuteShutdownAsync()
    {
        if (Interlocked.CompareExchange(ref _isShuttingDown, 1, 0) != 0)
        {
            // Shutdown sequence is already in progress or completed
            return;
        }

        ApproveShutdown();
        Debug.WriteLine("[ApplicationLifecycleService] Beginning coordinated application shutdown...");

        // 1. Stop active presentation safely
        try
        {
            await _presentationCoordinator.StopPresentationAsync(isShuttingDown: true).ConfigureAwait(false);
            Debug.WriteLine("[ApplicationLifecycleService] Active presentation stopped successfully.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ApplicationLifecycleService] Error stopping presentation during shutdown: {ex.Message}");
        }

        // 2. Stop live preview capture safely
        try
        {
            await _captureCoordinator.StopPreviewAsync().ConfigureAwait(false);
            Debug.WriteLine("[ApplicationLifecycleService] Preview capture stopped successfully.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ApplicationLifecycleService] Error stopping capture during shutdown: {ex.Message}");
        }

        // 3. Close secondary presentation output window
        try
        {
            _presentationWindowService.ClosePresentationWindow();
            Debug.WriteLine("[ApplicationLifecycleService] Presentation output window closed.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ApplicationLifecycleService] Error closing presentation window during shutdown: {ex.Message}");
        }

        // 4. Close secondary floating presenter dock window
        try
        {
            _presenterDockService.CloseDock();
            Debug.WriteLine("[ApplicationLifecycleService] Presenter dock window closed.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ApplicationLifecycleService] Error closing presenter dock during shutdown: {ex.Message}");
        }

        // 5. Unregister and dispose global hotkeys
        try
        {
            _hotkeyService.Dispose();
            Debug.WriteLine("[ApplicationLifecycleService] Global hotkeys unregistered and disposed.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ApplicationLifecycleService] Error disposing hotkeys during shutdown: {ex.Message}");
        }

        // 6. Save user settings
        try
        {
            await _settingsService.SaveSettingsAsync().ConfigureAwait(false);
            Debug.WriteLine("[ApplicationLifecycleService] User settings saved.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ApplicationLifecycleService] Error saving settings during shutdown: {ex.Message}");
        }

        Debug.WriteLine("[ApplicationLifecycleService] Coordinated application shutdown completed.");
    }
}
