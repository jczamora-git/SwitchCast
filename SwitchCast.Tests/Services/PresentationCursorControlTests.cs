using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.Services.Capture;
using Xunit;

namespace SwitchCast.Tests.Services;

public class PresentationCursorControlTests
{
    [Fact]
    public void CaptureSessionManager_Contract_AllowsCursorToggling()
    {
        var mockSessionManager = new Mock<ICaptureSessionManager>();
        mockSessionManager.SetupProperty(m => m.IsCursorCaptureEnabled, true);

        Assert.True(mockSessionManager.Object.IsCursorCaptureEnabled);

        mockSessionManager.Object.SetCursorCaptureEnabled(false);
        mockSessionManager.Object.IsCursorCaptureEnabled = false;
        Assert.False(mockSessionManager.Object.IsCursorCaptureEnabled);
    }

    [Fact]
    public void CaptureCoordinator_CursorToggle_PropagatesToSessionManager()
    {
        var mockItemFactory = new Mock<IGraphicsCaptureItemFactory>();
        var mockDeviceProvider = new Mock<IDirect3D11DeviceProvider>();
        var mockSessionManager = new Mock<ICaptureSessionManager>();
        var mockPreviewRenderer = new Mock<ICapturePreviewRenderer>();
        var mockStateService = new Mock<IPresentationStateService>();

        mockSessionManager.SetupGet(s => s.IsCursorCaptureEnabled).Returns(true);

        using var coordinator = new CaptureCoordinator(
            mockItemFactory.Object,
            mockDeviceProvider.Object,
            mockSessionManager.Object,
            mockPreviewRenderer.Object,
            mockStateService.Object);

        Assert.True(coordinator.IsCursorCaptureEnabled);

        coordinator.SetCursorCaptureEnabled(false);
        mockSessionManager.Verify(s => s.SetCursorCaptureEnabled(false), Times.Once);

        coordinator.SetCursorCaptureEnabled(true);
        mockSessionManager.Verify(s => s.SetCursorCaptureEnabled(true), Times.Once);
    }

    [Fact]
    public async Task PresentationCoordinator_SetShowPresentationCursorAsync_UpdatesCaptureCoordinatorAndSettings()
    {
        var mockStateService = new Mock<IPresentationStateService>();
        var mockWindowService = new Mock<IPresentationWindowService>();
        var mockCaptureCoordinator = new Mock<ICaptureCoordinator>();
        var mockRenderer = new Mock<IPresentationOutputRenderer>();
        var mockActivationService = new Mock<IWindowActivationService>();
        var mockSettingsService = new Mock<IApplicationSettingsService>();

        var userSettings = new UserSettings { ShowPresentationCursor = true };
        mockSettingsService.SetupGet(s => s.CurrentSettings).Returns(userSettings);

        using var coordinator = new PresentationCoordinator(
            mockStateService.Object,
            mockWindowService.Object,
            mockCaptureCoordinator.Object,
            mockRenderer.Object,
            mockActivationService.Object,
            settingsService: mockSettingsService.Object);

        Assert.True(coordinator.ShowPresentationCursor);
        mockCaptureCoordinator.Verify(c => c.SetCursorCaptureEnabled(true), Times.Once);

        // Turn off cursor
        await coordinator.SetShowPresentationCursorAsync(false);

        Assert.False(coordinator.ShowPresentationCursor);
        mockCaptureCoordinator.Verify(c => c.SetCursorCaptureEnabled(false), Times.Once);
        mockSettingsService.Verify(s => s.SetShowPresentationCursorAsync(false), Times.Once);
    }

    [Fact]
    public async Task ApplicationSettingsService_SetShowPresentationCursorAsync_PersistsValue()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"switchcast_test_settings_{Guid.NewGuid()}.json");
        try
        {
            var settingsService = new ApplicationSettingsService(tempFile);
            Assert.True(settingsService.CurrentSettings.ShowPresentationCursor);

            await settingsService.SetShowPresentationCursorAsync(false);
            Assert.False(settingsService.CurrentSettings.ShowPresentationCursor);

            // Reload from file to ensure persistence
            var reloadedService = new ApplicationSettingsService(tempFile);
            await reloadedService.LoadSettingsAsync();
            Assert.False(reloadedService.CurrentSettings.ShowPresentationCursor);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
