using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.Services.Camera;
using SwitchCast.Services.Capture;
using SwitchCast.Services.Media;
using SwitchCast.Services.PowerPoint;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.Services;

/// <summary>
/// Regression tests verifying the authoritative Presenter Dock close lifecycle,
/// single MainWindow activation guarantee, and prevention of process termination on Stop &amp; Close.
/// </summary>
public class DockCloseLifecycleHotfixTests
{
    private readonly Mock<IPresentationStateService> _mockStateService;
    private readonly Mock<IPresentationWindowService> _mockWindowService;
    private readonly Mock<ICaptureCoordinator> _mockCaptureCoordinator;
    private readonly Mock<IPresentationOutputRenderer> _mockOutputRenderer;
    private readonly Mock<IWindowActivationService> _mockWindowActivationService;
    private readonly Mock<IMediaPresentationService> _mockMediaPresentationService;
    private readonly Mock<INavigationService> _mockNavigationService;
    private readonly Mock<IPowerPointPresentationService> _mockPowerPointService;
    private readonly Mock<IPowerPointLiveService> _mockPowerPointLiveService;
    private readonly Mock<ICameraCaptureService> _mockCameraCaptureService;
    private readonly Mock<IPresenterDockService> _mockDockService;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;

    public DockCloseLifecycleHotfixTests()
    {
        _mockStateService = new Mock<IPresentationStateService>();
        _mockWindowService = new Mock<IPresentationWindowService>();
        _mockCaptureCoordinator = new Mock<ICaptureCoordinator>();
        _mockOutputRenderer = new Mock<IPresentationOutputRenderer>();
        _mockWindowActivationService = new Mock<IWindowActivationService>();
        _mockMediaPresentationService = new Mock<IMediaPresentationService>();
        _mockNavigationService = new Mock<INavigationService>();
        _mockPowerPointService = new Mock<IPowerPointPresentationService>();
        _mockPowerPointLiveService = new Mock<IPowerPointLiveService>();
        _mockCameraCaptureService = new Mock<ICameraCaptureService>();
        _mockDockService = new Mock<IPresenterDockService>();
        _mockSettingsService = new Mock<IApplicationSettingsService>();

        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockWindowService.SetupGet(w => w.IsWindowOpen).Returns(true);
        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Capturing);
        _mockWindowActivationService.Setup(w => w.ActivateMainWindow()).Returns(true);
        _mockSettingsService.SetupGet(s => s.CurrentSettings).Returns(new UserSettings());
    }

    private PresentationCoordinator CreateCoordinator()
    {
        return new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object,
            _mockNavigationService.Object,
            externalDisplayService: null,
            _mockPowerPointService.Object,
            _mockPowerPointLiveService.Object,
            _mockCameraCaptureService.Object);
    }

    [Fact]
    public async Task TestA_DockConfirmation_Cancel_PreservesPresentationAndDock()
    {
        var coordinator = CreateCoordinator();
        var vm = new PresenterDockViewModel(
            coordinator,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        bool dialogShown = false;
        vm.ConfirmCloseAsync = () =>
        {
            dialogShown = true;
            return Task.FromResult(false); // User clicked Cancel
        };

        await vm.CloseDockCommand.ExecuteAsync(null);

        Assert.True(dialogShown, "Confirmation dialog should be invoked.");
        _mockDockService.Verify(d => d.CloseDock(), Times.Never);
        _mockDockService.Verify(d => d.ShowDashboard(), Times.Never);
        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.Never);
        _mockWindowActivationService.Verify(w => w.ActivateMainWindow(), Times.Never);
    }

    [Fact]
    public async Task TestB_DockConfirmation_StopAndClose_StopsPresentationClosesDockAndPreservesSingleActivation()
    {
        var coordinator = CreateCoordinator();
        var vm = new PresenterDockViewModel(
            coordinator,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        bool dialogShown = false;
        vm.ConfirmCloseAsync = () =>
        {
            dialogShown = true;
            return Task.FromResult(true); // User clicked Stop & Close
        };

        await vm.CloseDockCommand.ExecuteAsync(null);

        Assert.True(dialogShown, "Confirmation dialog should be invoked.");

        // Authoritative StopPresentationAsync must be awaited and executed
        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.Once);
        _mockOutputRenderer.Verify(r => r.Clear(), Times.Once);

        // Dock must be closed
        _mockDockService.Verify(d => d.CloseDock(), Times.Once);

        // Single authoritative MainWindow activation (from StopPresentationAsync, never a duplicate from DockService)
        _mockWindowActivationService.Verify(w => w.ActivateMainWindow(), Times.Once);
        _mockDockService.Verify(d => d.ShowDashboard(), Times.Never);
        _mockNavigationService.Verify(n => n.NavigateToDashboard(null), Times.Once);
    }

    [Fact]
    public async Task TestC_StopFromDashboard_StopsPresentationAndActivatesMainWindowOnce()
    {
        var coordinator = CreateCoordinator();

        await coordinator.StopPresentationAsync(isShuttingDown: false);

        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.Once);
        _mockOutputRenderer.Verify(r => r.Clear(), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateMainWindow(), Times.Once);
        _mockNavigationService.Verify(n => n.NavigateToDashboard(null), Times.Once);
    }

    [Fact]
    public async Task TestD_IdleDock_ClosesWithoutConfirmation_AndShowsDashboard()
    {
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Idle);
        _mockWindowService.SetupGet(w => w.IsWindowOpen).Returns(false);
        var coordinator = CreateCoordinator();

        var vm = new PresenterDockViewModel(
            coordinator,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        bool dialogShown = false;
        vm.ConfirmCloseAsync = () =>
        {
            dialogShown = true;
            return Task.FromResult(true);
        };

        await vm.CloseDockCommand.ExecuteAsync(null);

        Assert.False(dialogShown, "Idle dock must close without confirmation.");
        _mockDockService.Verify(d => d.CloseDock(), Times.Once);
        _mockDockService.Verify(d => d.ShowDashboard(), Times.Once);
        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.Never);
    }

    [Fact]
    public async Task TestE_RepeatedCycles_StopAndClose_HandlesMultipleSourcesWithoutExceptions()
    {
        var coordinator = CreateCoordinator();
        var vm = new PresenterDockViewModel(
            coordinator,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        vm.ConfirmCloseAsync = () => Task.FromResult(true);

        for (int i = 0; i < 25; i++)
        {
            // Simulate start
            _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);

            // Execute Stop & Close
            await vm.CloseDockCommand.ExecuteAsync(null);
        }

        _mockDockService.Verify(d => d.CloseDock(), Times.Exactly(25));
        _mockDockService.Verify(d => d.ShowDashboard(), Times.Never);
    }

    [Fact]
    public void TestF_NativeWindowLifetime_WindowActivationServiceValidatesHandle()
    {
        var activationService = new Win32WindowActivationService();

        // 1. Unregistered handle
        Assert.False(activationService.ActivateMainWindow());

        // 2. Zero handle registered
        activationService.RegisterMainWindowHandle(IntPtr.Zero);
        Assert.False(activationService.ActivateMainWindow());

        // 3. Fake invalid handle (non-existent HWND 0x12345678)
        activationService.RegisterMainWindowHandle(new IntPtr(0x12345678));
        Assert.False(activationService.ActivateMainWindow());
    }

    [Fact]
    public async Task TestG_ApplicationShutdown_BypassesConfirmation_AndDoesNotReactivateDashboard()
    {
        var mockLifecycle = new Mock<IApplicationLifecycleService>();
        mockLifecycle.SetupGet(l => l.IsShuttingDown).Returns(true);

        var coordinator = CreateCoordinator();
        var vm = new PresenterDockViewModel(
            coordinator,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object,
            mockLifecycle.Object);

        bool dialogShown = false;
        vm.ConfirmCloseAsync = () =>
        {
            dialogShown = true;
            return Task.FromResult(true);
        };

        await vm.CloseDockCommand.ExecuteAsync(null);

        Assert.False(dialogShown, "Confirmation must be bypassed during shutdown.");
        _mockDockService.Verify(d => d.CloseDock(), Times.Once);
        _mockDockService.Verify(d => d.ShowDashboard(), Times.Never);
        _mockWindowActivationService.Verify(w => w.ActivateMainWindow(), Times.Never);
    }
}
