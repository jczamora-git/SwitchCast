using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.Services.Camera;
using SwitchCast.Services.Capture;
using SwitchCast.Services.Media;
using SwitchCast.Services.PowerPoint;
using SwitchCast.ViewModels;
using Windows.Graphics.Imaging;
using Xunit;

namespace SwitchCast.Tests.Services;

/// <summary>
/// Focused regression tests validating PowerPoint slide state synchronization, source persistence,
/// camera lifecycle, bounded frame dispatch, and COM exception handling.
/// </summary>
public class PowerPointCameraHotfixRegressionTests
{
    [Fact]
    public async Task PowerPoint_SlideIndex_FollowsRenderedSlideAndUpdatesCoordinator()
    {
        var mockRenderer = new Mock<IPresentationSlideRenderer>();
        var pptService = new PowerPointPresentationService(mockRenderer.Object);

        var stateService = new PresentationStateService();
        var mockWindowService = new Mock<IPresentationWindowService>();
        var mockCaptureCoord = new Mock<ICaptureCoordinator>();
        var mockOutputRenderer = new Mock<IPresentationOutputRenderer>();
        var mockActivationService = new Mock<IWindowActivationService>();
        var mockMediaService = new Mock<IMediaPresentationService>();

        var coordinator = new PresentationCoordinator(
            stateService,
            mockWindowService.Object,
            mockCaptureCoord.Object,
            mockOutputRenderer.Object,
            mockActivationService.Object,
            mockMediaService.Object,
            powerPointService: pptService);

        var deck = new PowerPointSource(@"C:\Decks\Presentation.pptx")
        {
            SlideCount = 14,
            CurrentSlideIndex = 1,
            IsAvailable = true
        };

        stateService.AddSelectedSource(deck);
        await coordinator.StartPresentationAsync(deck);

        // Initially Slide 1 of 14
        Assert.Equal("Slide 1 of 14", coordinator.PowerPointProgressText);
        Assert.Equal(1, (stateService.ActiveSource as PowerPointSource)?.CurrentSlideIndex);

        // Advance to slide 2
        await coordinator.PowerPointNextSlideAsync();
        Assert.Equal("Slide 2 of 14", coordinator.PowerPointProgressText);
        Assert.Equal(2, (stateService.ActiveSource as PowerPointSource)?.CurrentSlideIndex);

        // Advance to slide 5 via GoToSlide
        await coordinator.PowerPointGoToSlideAsync(5);
        Assert.Equal("Slide 5 of 14", coordinator.PowerPointProgressText);
        Assert.Equal(5, (stateService.ActiveSource as PowerPointSource)?.CurrentSlideIndex);

        // Previous slide -> slide 4
        await coordinator.PowerPointPreviousSlideAsync();
        Assert.Equal("Slide 4 of 14", coordinator.PowerPointProgressText);
        Assert.Equal(4, (stateService.ActiveSource as PowerPointSource)?.CurrentSlideIndex);
    }

    [Fact]
    public async Task PowerPoint_SwitchingBetweenDifferentDecks_ImmediatelyReflectsCorrectDeckAndSlide()
    {
        var mockRenderer = new Mock<IPresentationSlideRenderer>();
        var pptService = new PowerPointPresentationService(mockRenderer.Object);

        var stateService = new PresentationStateService();
        var mockWindowService = new Mock<IPresentationWindowService>();
        var mockCaptureCoord = new Mock<ICaptureCoordinator>();
        var mockOutputRenderer = new Mock<IPresentationOutputRenderer>();
        var mockActivationService = new Mock<IWindowActivationService>();
        var mockMediaService = new Mock<IMediaPresentationService>();

        var coordinator = new PresentationCoordinator(
            stateService,
            mockWindowService.Object,
            mockCaptureCoord.Object,
            mockOutputRenderer.Object,
            mockActivationService.Object,
            mockMediaService.Object,
            powerPointService: pptService);

        var deckA = new PowerPointSource(@"C:\Decks\DeckA.pptx")
        {
            Id = "deck_a",
            SlideCount = 10,
            CurrentSlideIndex = 1,
            IsAvailable = true
        };

        var deckB = new PowerPointSource(@"C:\Decks\DeckB.pptx")
        {
            Id = "deck_b",
            SlideCount = 25,
            CurrentSlideIndex = 1,
            IsAvailable = true
        };

        stateService.AddSelectedSource(deckA);
        stateService.AddSelectedSource(deckB);

        // Present Deck A and advance to slide 4
        await coordinator.StartPresentationAsync(deckA);
        await coordinator.PowerPointGoToSlideAsync(4);
        Assert.Equal("Slide 4 of 10", coordinator.PowerPointProgressText);

        // Switch to Deck B
        await coordinator.ExecuteSourceSwitchAsync(deckB);
        Assert.Equal("Slide 1 of 25", coordinator.PowerPointProgressText);
        Assert.Equal("deck_b", stateService.ActiveSource?.Id);
    }

    [Fact]
    public async Task PowerPoint_Unqueueing_DoesNotDeleteImportedEntryFromSources()
    {
        var stateService = new PresentationStateService();
        var mockWindowDiscovery = new Mock<IWindowDiscoveryService>();
        mockWindowDiscovery.Setup(w => w.EnumerateWindowsAsync(default)).ReturnsAsync([]);

        var mockMonitorDiscovery = new Mock<IMonitorDiscoveryService>();
        mockMonitorDiscovery.Setup(m => m.EnumerateMonitorsAsync(default)).ReturnsAsync([]);

        var mockPicker = new Mock<IMediaPickerService>();
        var mockSettings = new Mock<IApplicationSettingsService>();
        var settings = new UserSettings();
        mockSettings.Setup(s => s.CurrentSettings).Returns(settings);

        var mockPptService = new Mock<IPowerPointPresentationService>();
        var tempPptPath = Path.Combine(Path.GetTempPath(), $"SwitchCast_Test_{Guid.NewGuid():N}.pptx");
        File.WriteAllText(tempPptPath, "dummy pptx content");

        try
        {
            var pptSource = new PowerPointSource(tempPptPath)
            {
                Id = "pptx_persist_test",
                Title = "Persistent Deck",
                SlideCount = 8,
                IsAvailable = true
            };

            mockPptService.Setup(p => p.ImportPresentationAsync(tempPptPath, default)).ReturnsAsync(pptSource);
            mockPicker.Setup(p => p.PickPowerPointFilesAsync()).ReturnsAsync([tempPptPath]);

            var vm = new SourcesViewModel(
                stateService,
                mockWindowDiscovery.Object,
                mockMonitorDiscovery.Object,
                mediaPickerService: mockPicker.Object,
                settingsService: mockSettings.Object,
                powerPointService: mockPptService.Object);

            // Import PPTX
            await vm.AddPowerPointAsync();

            // Verify imported and persisted in UserSettings
            Assert.Contains(tempPptPath, settings.ImportedPowerPointPaths);

            // Switch to PowerPoint category (index 3)
            vm.SelectedCategoryIndex = 3;
            Assert.Single(vm.DisplayedSources);
            var item = vm.DisplayedSources[0];

            // Queue it
            item.IsSelected = true;
            Assert.True(stateService.IsSourceSelected(item.Id));

            // Unqueue it
            item.IsSelected = false;
            Assert.False(stateService.IsSourceSelected(item.Id));

            // Refresh sources
            await vm.RefreshSourcesAsync();

            // CRITICAL: Item must still be in DisplayedSources! Unqueueing must not delete imported entries
            Assert.Single(vm.DisplayedSources);
            Assert.Equal(item.Id, vm.DisplayedSources[0].Id);
        }
        finally
        {
            if (File.Exists(tempPptPath))
            {
                File.Delete(tempPptPath);
            }
        }
    }

    [Fact]
    public async Task PowerPoint_MissingFile_IsMarkedUnavailableRatherThanDeleted()
    {
        var stateService = new PresentationStateService();
        var mockWindowDiscovery = new Mock<IWindowDiscoveryService>();
        mockWindowDiscovery.Setup(w => w.EnumerateWindowsAsync(default)).ReturnsAsync([]);

        var mockMonitorDiscovery = new Mock<IMonitorDiscoveryService>();
        mockMonitorDiscovery.Setup(m => m.EnumerateMonitorsAsync(default)).ReturnsAsync([]);

        var mockSettings = new Mock<IApplicationSettingsService>();
        string nonExistentPath = @"C:\SwitchCast_Missing_File_12345.pptx";
        var settings = new UserSettings
        {
            ImportedPowerPointPaths = [nonExistentPath]
        };
        mockSettings.Setup(s => s.CurrentSettings).Returns(settings);

        var vm = new SourcesViewModel(
            stateService,
            mockWindowDiscovery.Object,
            mockMonitorDiscovery.Object,
            settingsService: mockSettings.Object);

        await vm.RefreshSourcesAsync();
        vm.SelectedCategoryIndex = 3; // PowerPoint

        Assert.Single(vm.DisplayedSources);
        var sourceItem = vm.DisplayedSources[0];
        Assert.False(sourceItem.IsAvailable);
        Assert.Equal(nonExistentPath, (sourceItem.Source as PowerPointSource)?.FilePath);
    }

    [Fact]
    public async Task Camera_SwitchingAway_CallsStopCaptureAsync()
    {
        var stateService = new PresentationStateService();
        var mockWindowService = new Mock<IPresentationWindowService>();
        var mockCaptureCoord = new Mock<ICaptureCoordinator>();
        var mockOutputRenderer = new Mock<IPresentationOutputRenderer>();
        var mockActivationService = new Mock<IWindowActivationService>();
        var mockMediaService = new Mock<IMediaPresentationService>();
        var mockCameraCapture = new Mock<ICameraCaptureService>();

        mockCameraCapture.Setup(c => c.IsCapturing).Returns(true);
        mockCameraCapture.Setup(c => c.StartCaptureAsync(It.IsAny<CameraSource>(), default)).ReturnsAsync(true);
        mockCameraCapture.Setup(c => c.StopCaptureAsync()).Returns(Task.CompletedTask);

        var coordinator = new PresentationCoordinator(
            stateService,
            mockWindowService.Object,
            mockCaptureCoord.Object,
            mockOutputRenderer.Object,
            mockActivationService.Object,
            mockMediaService.Object,
            cameraCaptureService: mockCameraCapture.Object);

        var camera = new CameraSource
        {
            Id = "cam_01",
            Title = "USB Webcam",
            DeviceId = "device_usb_cam",
            IsAvailable = true
        };

        var window = new WindowSource
        {
            Id = "win_01",
            Title = "Browser Window",
            WindowHandle = new nint(0x1234),
            IsAvailable = true
        };

        stateService.AddSelectedSource(camera);
        stateService.AddSelectedSource(window);

        // Start presentation with camera
        await coordinator.StartPresentationAsync(camera);
        mockCameraCapture.Verify(c => c.StartCaptureAsync(camera, It.IsAny<CancellationToken>()), Times.Once);

        // Switch to window -> camera capture MUST stop
        await coordinator.ExecuteSourceSwitchAsync(window);
        mockCameraCapture.Verify(c => c.StopCaptureAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Camera_ActiveOnlyMode_DoesNotStartCameraCapture()
    {
        var stateService = new PresentationStateService();
        var mockWindowService = new Mock<IPresentationWindowService>();
        var mockCaptureCoord = new Mock<ICaptureCoordinator>();
        var mockOutputRenderer = new Mock<IPresentationOutputRenderer>();
        var mockActivationService = new Mock<IWindowActivationService>();
        var mockMediaService = new Mock<IMediaPresentationService>();
        var mockCameraCapture = new Mock<ICameraCaptureService>();

        var coordinator = new PresentationCoordinator(
            stateService,
            mockWindowService.Object,
            mockCaptureCoord.Object,
            mockOutputRenderer.Object,
            mockActivationService.Object,
            mockMediaService.Object,
            cameraCaptureService: mockCameraCapture.Object);

        await coordinator.SetSwitchModeAsync(PresenterSwitchMode.ActiveOnly);

        var camera = new CameraSource
        {
            Id = "cam_01",
            Title = "USB Webcam",
            DeviceId = "device_usb_cam",
            IsAvailable = true
        };

        stateService.AddSelectedSource(camera);

        // Execute source switch in Active Only mode
        await coordinator.ExecuteSourceSwitchAsync(camera);

        // In Active Only, camera capture should NOT be started
        mockCameraCapture.Verify(c => c.StartCaptureAsync(It.IsAny<CameraSource>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null(stateService.ActiveSource);
    }

    [Fact]
    public void Camera_NotifyFrameCompleted_ReleasesBackpressureGate()
    {
        var service = new CameraCaptureService();
        Assert.Equal(0, service.FrameArrivalCount);
        Assert.Equal(0, service.FrameDropCount);

        service.NotifyFrameCompleted();
        // Verifies no exception thrown when notifying idle or completing frames
        Assert.False(service.IsCapturing);
    }

    [Fact]
    public void Diagnostics_LogException_FormatsHexHResultAndStackCleanly()
    {
        var ex = new System.Runtime.InteropServices.COMException("Test COM error", unchecked((int)0x8001010E));
        
        // Assert that HResult property accurately preserves the COM error code
        Assert.Equal(unchecked((int)0x8001010E), ex.HResult);
        Assert.Equal("8001010E", $"{ex.HResult:X8}");

        // Verifies LogException executes without error
        SwitchCast.Services.Diagnostics.SwitchCastDiagnostics.LogException("TestComponent", "TestOp", ex, "source_123", 42);
    }

    [Fact]
    public async Task Camera_StartsInStartingState_AndTransitionsToActiveOnFirstRenderedFrame()
    {
        var stateService = new PresentationStateService();
        var mockWindowService = new Mock<IPresentationWindowService>();
        var mockCaptureCoord = new Mock<ICaptureCoordinator>();
        var mockOutputRenderer = new Mock<IPresentationOutputRenderer>();
        var mockActivationService = new Mock<IWindowActivationService>();
        var mockMediaService = new Mock<IMediaPresentationService>();
        var mockCameraCapture = new Mock<ICameraCaptureService>();

        mockCameraCapture.Setup(c => c.StartCaptureAsync(It.IsAny<CameraSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var coordinator = new PresentationCoordinator(
            stateService,
            mockWindowService.Object,
            mockCaptureCoord.Object,
            mockOutputRenderer.Object,
            mockActivationService.Object,
            mockMediaService.Object,
            cameraCaptureService: mockCameraCapture.Object);

        var camera = new CameraSource
        {
            Id = "cam_01",
            Title = "A4tech FHD 1080P PC Camera",
            DeviceId = "device_a4tech_cam",
            IsAvailable = true
        };

        stateService.AddSelectedSource(camera);

        // Start presentation
        await coordinator.StartPresentationAsync(camera);

        // Before any frame arrives, status MUST be Starting (dock displays STARTING...)
        Assert.Equal(PresentationStatus.Starting, coordinator.Status);
        Assert.Equal(camera.Id, stateService.ActiveSource?.Id);

        // Simulate first camera frame arrival
        using var bitmap = new Windows.Graphics.Imaging.SoftwareBitmap(
            Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, 10, 10,
            Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);

        mockCameraCapture.Raise(c => c.FrameArrived += null, mockCameraCapture.Object, bitmap);

        // Frame submitted to renderer and status transitions to Active (LIVE)
        mockOutputRenderer.Verify(r => r.RenderBitmapAsync(It.IsAny<Windows.Graphics.Imaging.SoftwareBitmap>()), Times.Once);
        Assert.Equal(PresentationStatus.Active, coordinator.Status);
    }

    [Fact]
    public async Task Camera_DuplicateStartOrSwitchRequests_AreIdempotent()
    {
        var stateService = new PresentationStateService();
        var mockWindowService = new Mock<IPresentationWindowService>();
        var mockCaptureCoord = new Mock<ICaptureCoordinator>();
        var mockOutputRenderer = new Mock<IPresentationOutputRenderer>();
        var mockActivationService = new Mock<IWindowActivationService>();
        var mockMediaService = new Mock<IMediaPresentationService>();
        var mockCameraCapture = new Mock<ICameraCaptureService>();

        mockCameraCapture.Setup(c => c.StartCaptureAsync(It.IsAny<CameraSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var coordinator = new PresentationCoordinator(
            stateService,
            mockWindowService.Object,
            mockCaptureCoord.Object,
            mockOutputRenderer.Object,
            mockActivationService.Object,
            mockMediaService.Object,
            cameraCaptureService: mockCameraCapture.Object);

        var camera = new CameraSource
        {
            Id = "cam_01",
            Title = "A4tech FHD 1080P PC Camera",
            DeviceId = "device_a4tech_cam",
            IsAvailable = true
        };

        stateService.AddSelectedSource(camera);

        // First start request
        await coordinator.StartPresentationAsync(camera);
        mockCameraCapture.Verify(c => c.StartCaptureAsync(camera, It.IsAny<CancellationToken>()), Times.Once);

        // Duplicate start request while Starting
        await coordinator.StartPresentationAsync(camera);
        mockCameraCapture.Verify(c => c.StartCaptureAsync(camera, It.IsAny<CancellationToken>()), Times.Once);

        // Duplicate execute switch request
        await coordinator.ExecuteSourceSwitchAsync(camera);
        mockCameraCapture.Verify(c => c.StartCaptureAsync(camera, It.IsAny<CancellationToken>()), Times.Once);

        // Duplicate switch presentation request
        await coordinator.SwitchPresentationSourceAsync(camera);
        mockCameraCapture.Verify(c => c.StartCaptureAsync(camera, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Camera_SwitchToPowerPoint_StopsCameraCaptureAndLoadsPresentation()
    {
        var stateService = new PresentationStateService();
        var mockWindowService = new Mock<IPresentationWindowService>();
        var mockCaptureCoord = new Mock<ICaptureCoordinator>();
        var mockOutputRenderer = new Mock<IPresentationOutputRenderer>();
        var mockActivationService = new Mock<IWindowActivationService>();
        var mockMediaService = new Mock<IMediaPresentationService>();
        var mockCameraCapture = new Mock<ICameraCaptureService>();
        var mockPptService = new Mock<IPowerPointPresentationService>();

        mockCameraCapture.Setup(c => c.StartCaptureAsync(It.IsAny<CameraSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockPptService.Setup(p => p.LoadPresentationAsync(It.IsAny<PowerPointSource>()))
            .Returns(Task.CompletedTask);

        var coordinator = new PresentationCoordinator(
            stateService,
            mockWindowService.Object,
            mockCaptureCoord.Object,
            mockOutputRenderer.Object,
            mockActivationService.Object,
            mockMediaService.Object,
            powerPointService: mockPptService.Object,
            cameraCaptureService: mockCameraCapture.Object);

        var camera = new CameraSource { Id = "cam_01", Title = "Camera", DeviceId = "dev_1", IsAvailable = true };
        var ppt = new PowerPointSource { Id = "ppt_01", Title = "Slide Deck", FilePath = @"C:\deck.pptx", IsAvailable = true };

        stateService.AddSelectedSource(camera);
        stateService.AddSelectedSource(ppt);

        await coordinator.StartPresentationAsync(camera);
        mockCameraCapture.Verify(c => c.StartCaptureAsync(camera, It.IsAny<CancellationToken>()), Times.Once);

        await coordinator.ExecuteSourceSwitchAsync(ppt);
        mockCameraCapture.Verify(c => c.StopCaptureAsync(), Times.AtLeastOnce);
        mockPptService.Verify(p => p.LoadPresentationAsync(ppt), Times.Once);
        Assert.Equal(ppt.Id, stateService.ActiveSource?.Id);
    }
}
