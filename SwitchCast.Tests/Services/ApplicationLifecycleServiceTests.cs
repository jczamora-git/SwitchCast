using Moq;
using SwitchCast.Services;
using SwitchCast.Services.Capture;
using Xunit;

namespace SwitchCast.Tests.Services;

public class ApplicationLifecycleServiceTests
{
    private readonly Mock<IPresentationCoordinator> _mockPresentationCoordinator;
    private readonly Mock<ICaptureCoordinator> _mockCaptureCoordinator;
    private readonly Mock<IPresentationWindowService> _mockPresentationWindowService;
    private readonly Mock<IPresenterDockService> _mockPresenterDockService;
    private readonly Mock<IHotkeyService> _mockHotkeyService;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;

    public ApplicationLifecycleServiceTests()
    {
        _mockPresentationCoordinator = new Mock<IPresentationCoordinator>();
        _mockCaptureCoordinator = new Mock<ICaptureCoordinator>();
        _mockPresentationWindowService = new Mock<IPresentationWindowService>();
        _mockPresenterDockService = new Mock<IPresenterDockService>();
        _mockHotkeyService = new Mock<IHotkeyService>();
        _mockSettingsService = new Mock<IApplicationSettingsService>();
    }

    private ApplicationLifecycleService CreateService()
    {
        return new ApplicationLifecycleService(
            _mockPresentationCoordinator.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationWindowService.Object,
            _mockPresenterDockService.Object,
            _mockHotkeyService.Object,
            _mockSettingsService.Object);
    }

    [Fact]
    public void InitialState_IsCorrect()
    {
        var service = CreateService();

        Assert.False(service.IsShutdownApproved);
        Assert.False(service.IsShuttingDown);
        Assert.False(service.IsExitConfirmationOpen);
    }

    [Fact]
    public void ApproveShutdown_SetsIsShutdownApproved()
    {
        var service = CreateService();

        service.ApproveShutdown();

        Assert.True(service.IsShutdownApproved);
    }

    [Fact]
    public void IsExitConfirmationOpen_TracksStateCorrectly()
    {
        var service = CreateService();

        service.IsExitConfirmationOpen = true;
        Assert.True(service.IsExitConfirmationOpen);

        service.IsExitConfirmationOpen = false;
        Assert.False(service.IsExitConfirmationOpen);
    }

    [Fact]
    public async Task ExecuteShutdownAsync_CoordinatesCompleteTeardown()
    {
        var service = CreateService();

        await service.ExecuteShutdownAsync();

        Assert.True(service.IsShutdownApproved);
        Assert.True(service.IsShuttingDown);

        // Verify presentation and capture stopped
        _mockPresentationCoordinator.Verify(c => c.StopPresentationAsync(true), Times.Once);
        _mockCaptureCoordinator.Verify(c => c.StopPreviewAsync(), Times.Once);

        // Verify secondary windows closed
        _mockPresentationWindowService.Verify(w => w.ClosePresentationWindow(), Times.Once);
        _mockPresenterDockService.Verify(d => d.CloseDock(), Times.Once);

        // Verify hotkeys disposed
        _mockHotkeyService.Verify(h => h.Dispose(), Times.Once);

        // Verify settings saved
        _mockSettingsService.Verify(s => s.SaveSettingsAsync(), Times.Once);
    }

    [Fact]
    public async Task ExecuteShutdownAsync_IsIdempotent_ExecutesOnlyOnce()
    {
        var service = CreateService();

        await service.ExecuteShutdownAsync();
        await service.ExecuteShutdownAsync();

        _mockPresentationCoordinator.Verify(c => c.StopPresentationAsync(true), Times.Once);
        _mockCaptureCoordinator.Verify(c => c.StopPreviewAsync(), Times.Once);
        _mockPresentationWindowService.Verify(w => w.ClosePresentationWindow(), Times.Once);
        _mockPresenterDockService.Verify(d => d.CloseDock(), Times.Once);
        _mockHotkeyService.Verify(h => h.Dispose(), Times.Once);
        _mockSettingsService.Verify(s => s.SaveSettingsAsync(), Times.Once);
    }

    [Fact]
    public async Task ExecuteShutdownAsync_ResilientToServiceExceptions()
    {
        _mockPresentationCoordinator.Setup(c => c.StopPresentationAsync(It.IsAny<bool>())).ThrowsAsync(new InvalidOperationException("Simulated error"));
        _mockCaptureCoordinator.Setup(c => c.StopPreviewAsync()).ThrowsAsync(new Exception("Capture stop error"));

        var service = CreateService();

        // Must not throw
        await service.ExecuteShutdownAsync();

        // Remaining services must still be cleaned up
        _mockPresentationWindowService.Verify(w => w.ClosePresentationWindow(), Times.Once);
        _mockPresenterDockService.Verify(d => d.CloseDock(), Times.Once);
        _mockHotkeyService.Verify(h => h.Dispose(), Times.Once);
        _mockSettingsService.Verify(s => s.SaveSettingsAsync(), Times.Once);
    }
}
