using System.ComponentModel;
using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.Services.Capture;
using SwitchCast.Services.Media;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.ViewModels;

public class PresentationStartupAndThreadingTests
{
    private readonly Mock<IPresentationStateService> _mockStateService;
    private readonly Mock<INavigationService> _mockNavigationService;
    private readonly Mock<ICaptureCoordinator> _mockCaptureCoordinator;
    private readonly Mock<IPresentationCoordinator> _mockPresentationCoordinator;
    private readonly Mock<IMediaPresentationService> _mockMediaService;

    public PresentationStartupAndThreadingTests()
    {
        _mockStateService = new Mock<IPresentationStateService>();
        _mockNavigationService = new Mock<INavigationService>();
        _mockCaptureCoordinator = new Mock<ICaptureCoordinator>();
        _mockPresentationCoordinator = new Mock<IPresentationCoordinator>();
        _mockMediaService = new Mock<IMediaPresentationService>();

        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Idle);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns((CaptureSource?)null);
        _mockStateService.SetupGet(s => s.SelectedSourceCount).Returns(0);
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource>());

        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Idle);
        _mockCaptureCoordinator.SetupGet(c => c.CurrentPreviewSource).Returns((CaptureSource?)null);

        _mockPresentationCoordinator.SetupGet(p => p.Status).Returns(PresentationStatus.Idle);
        _mockPresentationCoordinator.SetupGet(p => p.IsOutputWindowOpen).Returns(false);
        _mockPresentationCoordinator.SetupGet(p => p.IsLive).Returns(false);
        _mockPresentationCoordinator.SetupGet(p => p.IsPaused).Returns(false);
        _mockPresentationCoordinator.SetupGet(p => p.IsBlackout).Returns(false);
        _mockPresentationCoordinator.SetupGet(p => p.CurrentPresentationSource).Returns((CaptureSource?)null);
        _mockPresentationCoordinator.SetupGet(p => p.MediaPresentationService).Returns(_mockMediaService.Object);
    }

    [Fact]
    public async Task Startup_NoQueuedSource_DoesNotStart_AndButtonDisabled()
    {
        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object);

        Assert.False(vm.CanStartPresentation);
        Assert.Null(vm.ResolveAuthoritativeStartSource());

        await vm.StartPresentationCommand.ExecuteAsync(null);

        _mockPresentationCoordinator.Verify(p => p.StartPresentationAsync(It.IsAny<CaptureSource>()), Times.Never);
    }

    [Fact]
    public async Task Startup_SingleQueuedSource_AutoResolvesAndStartsFirstSource()
    {
        var chrome = new WindowSource { Id = "win-chrome", Title = "Google Chrome", IsAvailable = true };
        var sources = new List<CaptureSource> { chrome };

        _mockStateService.SetupGet(s => s.SelectedSources).Returns(sources);
        _mockStateService.SetupGet(s => s.SelectedSourceCount).Returns(1);

        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object);

        Assert.True(vm.CanStartPresentation);
        var target = vm.ResolveAuthoritativeStartSource();
        Assert.NotNull(target);
        Assert.Equal("win-chrome", target.Id);

        await vm.StartPresentationCommand.ExecuteAsync(null);

        _mockPresentationCoordinator.Verify(p => p.StartPresentationAsync(chrome), Times.Once);
        Assert.Equal(chrome, vm.SelectedPresentationSource);
    }

    [Fact]
    public async Task Startup_MultipleQueuedSources_DefaultsToFirstQueuedSource_WhenNoSelection()
    {
        var chrome = new WindowSource { Id = "win-chrome", Title = "Google Chrome", IsAvailable = true };
        var video = new VideoMediaSource { Id = "vid-demo", Title = "Demo.mp4", FilePath = @"C:\videos\demo.mp4", IsAvailable = true };
        var slides = new PowerPointSource(@"C:\slides\deck.pptx") { Id = "ppt-deck", Title = "Slides.pptx", IsAvailable = true };
        var webcam = new CameraSource { Id = "cam-1", Title = "Integrated Webcam", DeviceId = "cam-1", IsAvailable = true };
        var sources = new List<CaptureSource> { chrome, video, slides, webcam };

        _mockStateService.SetupGet(s => s.SelectedSources).Returns(sources);
        _mockStateService.SetupGet(s => s.SelectedSourceCount).Returns(4);

        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object);

        // Click Start Presenting once without changing dropdown
        await vm.StartPresentationCommand.ExecuteAsync(null);

        // Authoritative resolution must pick Chrome (the first queued item)
        _mockPresentationCoordinator.Verify(p => p.StartPresentationAsync(chrome), Times.Once);
        Assert.Equal("win-chrome", vm.SelectedPresentationSource?.Id);
    }

    [Fact]
    public async Task Startup_ExplicitlySelectedSecondQueueItem_StartsSelectedSecondItem()
    {
        var chrome = new WindowSource { Id = "win-chrome", Title = "Google Chrome", IsAvailable = true };
        var video = new VideoMediaSource { Id = "vid-demo", Title = "Demo.mp4", FilePath = @"C:\videos\demo.mp4", IsAvailable = true };
        var sources = new List<CaptureSource> { chrome, video };

        _mockStateService.SetupGet(s => s.SelectedSources).Returns(sources);
        _mockStateService.SetupGet(s => s.SelectedSourceCount).Returns(2);

        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object);

        // User explicitly selects the second item (video)
        vm.SelectedPresentationSource = video;

        await vm.StartPresentationCommand.ExecuteAsync(null);

        // Must start Demo.mp4, never silently substituting Chrome
        _mockPresentationCoordinator.Verify(p => p.StartPresentationAsync(video), Times.Once);
        Assert.Equal("vid-demo", vm.SelectedPresentationSource?.Id);
    }

    [Fact]
    public async Task Startup_StaleSelectedSource_FallsBackToFirstAvailableQueuedSource()
    {
        var stale = new WindowSource { Id = "win-stale", Title = "Closed App", IsAvailable = true };
        var fresh = new WindowSource { Id = "win-fresh", Title = "Fresh App", IsAvailable = true };
        var sources = new List<CaptureSource> { fresh };

        _mockStateService.SetupGet(s => s.SelectedSources).Returns(sources);
        _mockStateService.SetupGet(s => s.SelectedSourceCount).Returns(1);

        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object)
        {
            SelectedPresentationSource = stale // Stale reference not in current queue
        };

        var target = vm.ResolveAuthoritativeStartSource();
        Assert.Equal("win-fresh", target?.Id);

        await vm.StartPresentationCommand.ExecuteAsync(null);

        _mockPresentationCoordinator.Verify(p => p.StartPresentationAsync(fresh), Times.Once);
    }

    [Fact]
    public async Task Startup_UnavailableTargetSource_DoesNotStart()
    {
        var offlineCamera = new CameraSource { Id = "cam-offline", Title = "Unplugged Cam", DeviceId = "cam-offline", IsAvailable = false };
        var sources = new List<CaptureSource> { offlineCamera };

        _mockStateService.SetupGet(s => s.SelectedSources).Returns(sources);
        _mockStateService.SetupGet(s => s.SelectedSourceCount).Returns(1);

        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object);

        var target = vm.ResolveAuthoritativeStartSource();
        Assert.Null(target);

        await vm.StartPresentationCommand.ExecuteAsync(null);

        _mockPresentationCoordinator.Verify(p => p.StartPresentationAsync(It.IsAny<CaptureSource>()), Times.Never);
    }

    [Fact]
    public async Task Startup_StartClickedTwice_IsIdempotentAndGuardedWhileStarting()
    {
        var source = new WindowSource { Id = "win-1", Title = "PowerPoint", IsAvailable = true };
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource> { source });
        _mockStateService.SetupGet(s => s.SelectedSourceCount).Returns(1);

        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object);

        // When status transitions to Starting, Start button is disabled
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Starting);
        _mockStateService.Raise(s => s.PropertyChanged += null, new PropertyChangedEventArgs(nameof(IPresentationStateService.Status)));

        Assert.True(vm.IsStarting);
        Assert.False(vm.CanStartPresentation);
        Assert.Equal("Presentation starting...", vm.StartPresentingTooltip);

        // Attempting to execute while starting should be a safe no-op
        await vm.StartPresentationCommand.ExecuteAsync(null);
        _mockPresentationCoordinator.Verify(p => p.StartPresentationAsync(It.IsAny<CaptureSource>()), Times.Never);
    }

    [Fact]
    public async Task Startup_FirstVideoSource_StartsAuthoritativeVideoSource()
    {
        var video = new VideoMediaSource { Id = "vid-1", Title = "Clip.mp4", FilePath = @"C:\videos\clip.mp4", IsAvailable = true };
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource> { video });
        _mockStateService.SetupGet(s => s.SelectedSourceCount).Returns(1);

        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object);

        await vm.StartPresentationCommand.ExecuteAsync(null);

        _mockPresentationCoordinator.Verify(p => p.StartPresentationAsync(video), Times.Once);
    }

    [Fact]
    public async Task Startup_FirstPowerPointSource_StartsAuthoritativePowerPointSource()
    {
        var ppt = new PowerPointSource(@"C:\slides\talk.pptx") { Id = "ppt-1", Title = "Talk.pptx", IsAvailable = true };
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource> { ppt });
        _mockStateService.SetupGet(s => s.SelectedSourceCount).Returns(1);

        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object);

        await vm.StartPresentationCommand.ExecuteAsync(null);

        _mockPresentationCoordinator.Verify(p => p.StartPresentationAsync(ppt), Times.Once);
    }

    [Fact]
    public async Task Startup_FirstCameraSource_StartsAuthoritativeCameraSource()
    {
        var camera = new CameraSource { Id = "cam-1", Title = "Webcam", DeviceId = "cam-1", IsAvailable = true };
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource> { camera });
        _mockStateService.SetupGet(s => s.SelectedSourceCount).Returns(1);

        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object);

        await vm.StartPresentationCommand.ExecuteAsync(null);

        _mockPresentationCoordinator.Verify(p => p.StartPresentationAsync(camera), Times.Once);
    }

    [Fact]
    public void Threading_OffThreadStateChange_EnqueuedSafelyViaDispatcher()
    {
        var mockDispatcher = new Mock<IUIThreadDispatcher>();
        mockDispatcher.SetupGet(d => d.HasThreadAccess).Returns(false);
        Action? enqueuedAction = null;
        mockDispatcher.Setup(d => d.TryEnqueue(It.IsAny<Action>()))
            .Callback<Action>(a => enqueuedAction = a)
            .Returns(true);

        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object,
            uiDispatcher: mockDispatcher.Object);

        // Raise state change simulating worker thread (HasThreadAccess = false)
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockStateService.Raise(s => s.PropertyChanged += null, new PropertyChangedEventArgs(nameof(IPresentationStateService.Status)));

        // Must have marshaled via TryEnqueue
        mockDispatcher.Verify(d => d.TryEnqueue(It.IsAny<Action>()), Times.Once);
        Assert.NotNull(enqueuedAction);

        // Execute enqueued action on owning UI thread
        var notifiedProps = new List<string>();
        vm.PropertyChanged += (s, e) => notifiedProps.Add(e.PropertyName!);
        enqueuedAction();

        Assert.Contains(nameof(DashboardViewModel.StatusDisplayText), notifiedProps);
        Assert.Contains(nameof(DashboardViewModel.IsPresenting), notifiedProps);
    }

    [Fact]
    public void Threading_PresentationViewModel_MarshalsOffThreadStatusToUIThread()
    {
        var mockDispatcher = new Mock<IUIThreadDispatcher>();
        mockDispatcher.SetupGet(d => d.HasThreadAccess).Returns(false);
        Action? enqueuedAction = null;
        mockDispatcher.Setup(d => d.TryEnqueue(It.IsAny<Action>()))
            .Callback<Action>(a => enqueuedAction = a)
            .Returns(true);

        var vm = new PresentationViewModel(
            _mockStateService.Object,
            _mockPresentationCoordinator.Object,
            uiDispatcher: mockDispatcher.Object);

        // Simulating camera frame thread changing status to Active
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockStateService.Raise(s => s.PropertyChanged += null, new PropertyChangedEventArgs(nameof(IPresentationStateService.Status)));

        mockDispatcher.Verify(d => d.TryEnqueue(It.IsAny<Action>()), Times.Once);
        Assert.NotNull(enqueuedAction);

        var notifiedProps = new List<string>();
        vm.PropertyChanged += (s, e) => notifiedProps.Add(e.PropertyName!);
        enqueuedAction();

        Assert.Contains(nameof(PresentationViewModel.ScreenCaptureVisibility), notifiedProps);
        Assert.Contains(nameof(PresentationViewModel.StandbyVisibility), notifiedProps);
    }

    [Fact]
    public void Threading_DockViewModel_MarshalsOffThreadEventWithoutDirectFallback()
    {
        var mockWindowService = new Mock<IPresentationWindowService>();
        var mockDockService = new Mock<IPresenterDockService>();
        var mockSettingsService = new Mock<IApplicationSettingsService>();

        var mockDispatcher = new Mock<IUIThreadDispatcher>();
        mockDispatcher.SetupGet(d => d.HasThreadAccess).Returns(false);
        Action? enqueuedAction = null;
        mockDispatcher.Setup(d => d.TryEnqueue(It.IsAny<Action>()))
            .Callback<Action>(a => enqueuedAction = a)
            .Returns(true);

        var vm = new PresenterDockViewModel(
            _mockPresentationCoordinator.Object,
            _mockStateService.Object,
            mockWindowService.Object,
            mockDockService.Object,
            mockSettingsService.Object,
            uiDispatcher: mockDispatcher.Object);

        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockStateService.Raise(s => s.PropertyChanged += null, new PropertyChangedEventArgs(nameof(IPresentationStateService.Status)));

        mockDispatcher.Verify(d => d.TryEnqueue(It.IsAny<Action>()), Times.Once);
        Assert.NotNull(enqueuedAction);

        var notifiedProps = new List<string>();
        vm.PropertyChanged += (s, e) => notifiedProps.Add(e.PropertyName!);
        enqueuedAction();

        Assert.Contains(nameof(PresenterDockViewModel.Status), notifiedProps);
    }

    [Fact]
    public void Threading_FailedTryEnqueue_DropsGracefullyWithoutCrashing()
    {
        var mockDispatcher = new Mock<IUIThreadDispatcher>();
        mockDispatcher.SetupGet(d => d.HasThreadAccess).Returns(false);
        // TryEnqueue fails (window closing/queue shutting down)
        mockDispatcher.Setup(d => d.TryEnqueue(It.IsAny<Action>())).Returns(false);

        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object,
            uiDispatcher: mockDispatcher.Object);

        // Must not throw or invoke action directly on worker thread
        _mockStateService.Raise(s => s.PropertyChanged += null, new PropertyChangedEventArgs(nameof(IPresentationStateService.Status)));

        mockDispatcher.Verify(d => d.TryEnqueue(It.IsAny<Action>()), Times.Once);
    }

    [Fact]
    public void Threading_DisposedViewModel_SafelyIgnoresIncomingOffThreadEvents()
    {
        var mockDispatcher = new Mock<IUIThreadDispatcher>();
        mockDispatcher.SetupGet(d => d.HasThreadAccess).Returns(false);

        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object,
            uiDispatcher: mockDispatcher.Object);

        vm.Dispose();

        _mockStateService.Raise(s => s.PropertyChanged += null, new PropertyChangedEventArgs(nameof(IPresentationStateService.Status)));

        // After dispose, no updates are enqueued
        mockDispatcher.Verify(d => d.TryEnqueue(It.IsAny<Action>()), Times.Never);
    }

    [Fact]
    public async Task Threading_MediaStateEvent_OffThreadSubscriberDoesNotBreakVideoPlayback()
    {
        var mediaService = new MediaPresentationService();
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_video_{Guid.NewGuid():N}.mp4");
        File.WriteAllBytes(tempFile, new byte[100]);

        try
        {
            // Attach a subscriber that throws an exception simulating an off-thread XAML binding failure
            mediaService.MediaStateChanged += (s, e) => throw new System.Runtime.InteropServices.COMException("RPC_E_WRONG_THREAD", unchecked((int)0x8001010E));

            var video = new VideoMediaSource { Id = "vid-test", Title = "Test.mp4", FilePath = tempFile, IsAvailable = true };
            await mediaService.PlayVideoAsync(video);

            // LastErrorMessage must NOT be contaminated with "Failed to play video (0x8001010E)"
            Assert.NotEqual("Failed to play video (0x8001010E): RPC_E_WRONG_THREAD", mediaService.LastErrorMessage);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
            mediaService.Dispose();
        }
    }

    [Fact]
    public void Threading_ActiveSourceChanged_OnWorkerThread_EnqueuesSelectedPresentationSourceOnUIDispatcher()
    {
        var mockDispatcher = new Mock<IUIThreadDispatcher>();
        Action? enqueuedAction = null;
        mockDispatcher.SetupGet(d => d.HasThreadAccess).Returns(false);
        mockDispatcher.Setup(d => d.TryEnqueue(It.IsAny<Action>()))
            .Callback<Action>(act => enqueuedAction = act)
            .Returns(true);

        var pptSource = new PowerPointSource
        {
            Id = "ppt-demo",
            Title = "Demo.pptx",
            FilePath = @"C:\Presentations\Demo.pptx",
            SlideCount = 5,
            CurrentSlideIndex = 1,
            IsAvailable = true
        };

        _mockStateService.SetupGet(s => s.ActiveSource).Returns(pptSource);
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource> { pptSource });

        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object,
            uiDispatcher: mockDispatcher.Object);

        // Raise ActiveSource property change from a simulated background worker thread
        _mockStateService.Raise(s => s.PropertyChanged += null, new PropertyChangedEventArgs(nameof(IPresentationStateService.ActiveSource)));

        // Must marshal via dispatcher
        mockDispatcher.Verify(d => d.TryEnqueue(It.IsAny<Action>()), Times.AtLeastOnce);
        Assert.NotNull(enqueuedAction);

        // Execute the enqueued action as the UI thread would
        enqueuedAction();

        // SelectedPresentationSource must now safely equal pptSource
        Assert.Equal(pptSource, vm.SelectedPresentationSource);
    }

    [Fact]
    public async Task PowerPoint_Video_Camera_Switching_20Cycles_MaintainsReliableState()
    {
        var pptSource = new PowerPointSource
        {
            Id = "ppt-1",
            Title = "Demo.pptx",
            FilePath = @"C:\Presentations\Demo.pptx",
            SlideCount = 10,
            CurrentSlideIndex = 1,
            IsAvailable = true
        };

        var videoSource = new VideoMediaSource
        {
            Id = "vid-1",
            Title = "Video.mp4",
            FilePath = @"C:\Media\Video.mp4",
            IsAvailable = true
        };

        var cameraSource = new CameraSource
        {
            Id = "cam-1",
            Title = "Integrated Webcam",
            DeviceId = "cam-device-1",
            IsAvailable = true
        };

        var queue = new List<CaptureSource> { pptSource, videoSource, cameraSource };
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(queue);
        _mockStateService.SetupGet(s => s.SelectedSourceCount).Returns(queue.Count);

        _mockPresentationCoordinator.SetupGet(p => p.IsLive).Returns(true);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);

        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object);

        // Execute 20 cycles of: PowerPoint -> Video -> Camera -> PowerPoint
        for (int cycle = 1; cycle <= 20; cycle++)
        {
            // Switch to Video
            _mockStateService.SetupGet(s => s.ActiveSource).Returns(videoSource);
            await vm.SwitchPresentationSourceCommand.ExecuteAsync(videoSource);
            _mockPresentationCoordinator.Verify(p => p.SwitchPresentationSourceAsync(videoSource), Times.Exactly(cycle));

            // Switch to Camera
            _mockStateService.SetupGet(s => s.ActiveSource).Returns(cameraSource);
            await vm.SwitchPresentationSourceCommand.ExecuteAsync(cameraSource);
            _mockPresentationCoordinator.Verify(p => p.SwitchPresentationSourceAsync(cameraSource), Times.Exactly(cycle));

            // Switch back to PowerPoint
            _mockStateService.SetupGet(s => s.ActiveSource).Returns(pptSource);
            await vm.SwitchPresentationSourceCommand.ExecuteAsync(pptSource);
            _mockPresentationCoordinator.Verify(p => p.SwitchPresentationSourceAsync(pptSource), Times.Exactly(cycle));

            // Verify pptSource remains available and queued
            Assert.True(pptSource.IsAvailable);
            Assert.Contains(pptSource, queue);
        }
    }
}
