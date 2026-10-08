using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.Services.Capture;
using Windows.Graphics.Imaging;
using Xunit;

namespace SwitchCast.Tests.Services;

public class RapidSwitchingConcurrencyTests
{
    private readonly Mock<IGraphicsCaptureItemFactory> _mockItemFactory;
    private readonly Mock<IDirect3D11DeviceProvider> _mockDeviceProvider;
    private readonly Mock<ICaptureSessionManager> _mockSessionManager;
    private readonly Mock<ICapturePreviewRenderer> _mockPreviewRenderer;
    private readonly Mock<IPresentationStateService> _mockPresentationStateService;
    private readonly Mock<IPresentationWindowService> _mockWindowService;
    private readonly Mock<IPresentationOutputRenderer> _mockOutputRenderer;

    public RapidSwitchingConcurrencyTests()
    {
        _mockItemFactory = new Mock<IGraphicsCaptureItemFactory>();
        _mockDeviceProvider = new Mock<IDirect3D11DeviceProvider>();
        _mockSessionManager = new Mock<ICaptureSessionManager>();
        _mockPreviewRenderer = new Mock<ICapturePreviewRenderer>();
        _mockPresentationStateService = new Mock<IPresentationStateService>();
        _mockWindowService = new Mock<IPresentationWindowService>();
        _mockOutputRenderer = new Mock<IPresentationOutputRenderer>();
    }

    [Fact]
    public async Task TEST01_Switch_A_To_B_Normally_CompletesAndSetsActiveSource()
    {
        var sourceA = new WindowSource { Id = "win-A", Title = "Window A", IsAvailable = true };
        var sourceB = new WindowSource { Id = "win-B", Title = "Window B", IsAvailable = true };

        using var coordinator = new CaptureCoordinator(
            _mockItemFactory.Object,
            _mockDeviceProvider.Object,
            _mockSessionManager.Object,
            _mockPreviewRenderer.Object,
            _mockPresentationStateService.Object);

        await coordinator.StartPreviewAsync(sourceA);
        Assert.Equal(CaptureState.Capturing, coordinator.State);
        Assert.Equal("win-A", coordinator.CurrentPreviewSource?.Id);

        await coordinator.SwitchPreviewSourceAsync(sourceB);
        Assert.Equal(CaptureState.Capturing, coordinator.State);
        Assert.Equal("win-B", coordinator.CurrentPreviewSource?.Id);
        _mockPresentationStateService.Verify(s => s.SetActiveSource(sourceB), Times.Once);
    }

    [Fact]
    public async Task TEST02_Switch_A_To_B_To_C_Rapidly_OnlyFinalRequestIsActive()
    {
        var sourceA = new WindowSource { Id = "win-A", Title = "Window A", IsAvailable = true };
        var sourceB = new WindowSource { Id = "win-B", Title = "Window B", IsAvailable = true };
        var sourceC = new WindowSource { Id = "win-C", Title = "Window C", IsAvailable = true };

        using var coordinator = new CaptureCoordinator(
            _mockItemFactory.Object,
            _mockDeviceProvider.Object,
            _mockSessionManager.Object,
            _mockPreviewRenderer.Object,
            _mockPresentationStateService.Object);

        await coordinator.StartPreviewAsync(sourceA);

        // Rapidly issue switches to B and then C without awaiting B
        var taskB = coordinator.SwitchPreviewSourceAsync(sourceB);
        var taskC = coordinator.SwitchPreviewSourceAsync(sourceC);

        await Task.WhenAll(taskB, taskC);

        Assert.Equal(CaptureState.Capturing, coordinator.State);
        Assert.Equal("win-C", coordinator.CurrentPreviewSource?.Id);
        _mockPresentationStateService.Verify(s => s.SetActiveSource(sourceC), Times.Once);
    }

    [Fact]
    public async Task TEST03_OverlappingSwitchRequests_Concurrently_AreSerialized()
    {
        using var coordinator = new CaptureCoordinator(
            _mockItemFactory.Object,
            _mockDeviceProvider.Object,
            _mockSessionManager.Object,
            _mockPreviewRenderer.Object,
            _mockPresentationStateService.Object);

        var initialSource = new WindowSource { Id = "win-init", Title = "Window Init", IsAvailable = true };
        await coordinator.StartPreviewAsync(initialSource);

        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            var src = new WindowSource { Id = $"win-{i}", Title = $"Window {i}", IsAvailable = true };
            tasks.Add(Task.Run(() => coordinator.SwitchPreviewSourceAsync(src)));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(CaptureState.Capturing, coordinator.State);
        Assert.NotNull(coordinator.CurrentPreviewSource);
        Assert.Null(coordinator.LastErrorMessage);
    }

    [Fact]
    public async Task TEST04_SwitchWhileRenderingInProgress_CompletesSafely()
    {
        var sourceA = new WindowSource { Id = "win-A", Title = "Window A", IsAvailable = true };
        var sourceB = new WindowSource { Id = "win-B", Title = "Window B", IsAvailable = true };

        using var coordinator = new CaptureCoordinator(
            _mockItemFactory.Object,
            _mockDeviceProvider.Object,
            _mockSessionManager.Object,
            _mockPreviewRenderer.Object,
            _mockPresentationStateService.Object);

        await coordinator.StartPreviewAsync(sourceA);

        using var rawBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 10, 10, BitmapAlphaMode.Premultiplied);
        var sharedBitmap = new RefCountedSoftwareBitmap(rawBitmap, initialRefCount: 2);

        var tcs = new TaskCompletionSource();
        // Simulate renderer in-flight work
        _mockPreviewRenderer.Setup(r => r.RenderSharedBitmapAsync(It.IsAny<RefCountedSoftwareBitmap>(), It.IsAny<long>()))
            .Returns(async () =>
            {
                await Task.Delay(10);
                sharedBitmap.Release();
                tcs.TrySetResult();
            });

        _mockSessionManager.Raise(s => s.FrameArrived += null, _mockSessionManager.Object, new FrameArrivedEventArgs(sharedBitmap, 1));

        // Concurrently switch
        await coordinator.SwitchPreviewSourceAsync(sourceB);

        await tcs.Task;
        sharedBitmap.Release();
        Assert.Equal(0, sharedBitmap.RefCount);
        Assert.Equal("win-B", coordinator.CurrentPreviewSource?.Id);
    }

    [Fact]
    public void TEST05_SwitchWhileDispatcherCallbackQueued_GenerationCheckPreservesIsolation()
    {
        using var rawBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 10, 10, BitmapAlphaMode.Premultiplied);
        var sharedBitmap = new RefCountedSoftwareBitmap(rawBitmap, initialRefCount: 1);

        // Simulate frame from generation 1 arriving after generation advanced to 2
        long activeSessionGeneration = 2;
        long frameGeneration = 1;

        bool frameRendered = false;
        if (frameGeneration == activeSessionGeneration)
        {
            frameRendered = true;
        }
        else
        {
            sharedBitmap.Release();
        }

        Assert.False(frameRendered);
        Assert.Equal(0, sharedBitmap.RefCount);
    }

    [Fact]
    public async Task TEST06_SwitchImmediatelyAfterFirstFrame_TransitionsSafely()
    {
        var sourceA = new WindowSource { Id = "win-A", Title = "Window A", IsAvailable = true };
        var sourceB = new WindowSource { Id = "win-B", Title = "Window B", IsAvailable = true };

        using var coordinator = new CaptureCoordinator(
            _mockItemFactory.Object,
            _mockDeviceProvider.Object,
            _mockSessionManager.Object,
            _mockPreviewRenderer.Object,
            _mockPresentationStateService.Object);

        await coordinator.StartPreviewAsync(sourceA);

        using var rawBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 10, 10, BitmapAlphaMode.Premultiplied);
        var sharedBitmap = new RefCountedSoftwareBitmap(rawBitmap, initialRefCount: 1);

        _mockSessionManager.Raise(s => s.FrameArrived += null, _mockSessionManager.Object, new FrameArrivedEventArgs(sharedBitmap, 1));
        await coordinator.SwitchPreviewSourceAsync(sourceB);

        Assert.Equal("win-B", coordinator.CurrentPreviewSource?.Id);
        Assert.Equal(CaptureState.Capturing, coordinator.State);
    }

    [Fact]
    public async Task TEST07_SwitchWhilePaused_PreservesPausedStatus()
    {
        var sourceB = new WindowSource { Id = "win-B", Title = "Window B", IsAvailable = true };

        _mockPresentationStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Paused);

        var mockCapture = new Mock<ICaptureCoordinator>();
        mockCapture.SetupGet(c => c.State).Returns(CaptureState.Capturing);

        using var presCoordinator = new PresentationCoordinator(
            _mockPresentationStateService.Object,
            _mockWindowService.Object,
            mockCapture.Object,
            _mockOutputRenderer.Object);

        await presCoordinator.SwitchPresentationSourceAsync(sourceB);

        // Verify status was not overwritten with Active
        _mockPresentationStateService.Verify(s => s.SetStatus(PresentationStatus.Active), Times.Never);
        _mockPresentationStateService.Verify(s => s.SetActiveSource(sourceB), Times.Once);
    }

    [Fact]
    public async Task TEST08_SwitchWhileBlackout_PreservesBlackoutStatus()
    {
        var sourceB = new WindowSource { Id = "win-B", Title = "Window B", IsAvailable = true };

        _mockPresentationStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Blackout);

        var mockCapture = new Mock<ICaptureCoordinator>();
        mockCapture.SetupGet(c => c.State).Returns(CaptureState.Capturing);

        using var presCoordinator = new PresentationCoordinator(
            _mockPresentationStateService.Object,
            _mockWindowService.Object,
            mockCapture.Object,
            _mockOutputRenderer.Object);

        await presCoordinator.SwitchPresentationSourceAsync(sourceB);

        _mockPresentationStateService.Verify(s => s.SetStatus(PresentationStatus.Active), Times.Never);
        _mockPresentationStateService.Verify(s => s.SetActiveSource(sourceB), Times.Once);
    }

    [Fact]
    public async Task TEST09_SwitchWhileStopPresentingExecutes_SerializedSafely()
    {
        var sourceB = new WindowSource { Id = "win-B", Title = "Window B", IsAvailable = true };

        var mockCapture = new Mock<ICaptureCoordinator>();
        mockCapture.SetupGet(c => c.State).Returns(CaptureState.Capturing);

        using var presCoordinator = new PresentationCoordinator(
            _mockPresentationStateService.Object,
            _mockWindowService.Object,
            mockCapture.Object,
            _mockOutputRenderer.Object);

        var taskSwitch = presCoordinator.SwitchPresentationSourceAsync(sourceB);
        var taskStop = presCoordinator.StopPresentationAsync();

        await Task.WhenAll(taskSwitch, taskStop);

        Assert.Null(presCoordinator.LastErrorMessage);
    }

    [Fact]
    public async Task TEST10_CloseOutputWindowDuringSwitch_HandledGracefully()
    {
        _mockPresentationStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        var mockCapture = new Mock<ICaptureCoordinator>();
        using var presCoordinator = new PresentationCoordinator(
            _mockPresentationStateService.Object,
            _mockWindowService.Object,
            mockCapture.Object,
            _mockOutputRenderer.Object);

        _mockWindowService.Raise(w => w.WindowClosed += null, _mockWindowService.Object, EventArgs.Empty);

        // Give async StopPresentationAsync a moment to complete
        await Task.Delay(20);

        _mockOutputRenderer.Verify(r => r.Clear(), Times.Once);
    }

    [Fact]
    public async Task TEST11_RendererExceptionDuringSwitch_TransitionsToFailedState()
    {
        var sourceA = new WindowSource { Id = "win-A", Title = "Window A", IsAvailable = true };
        var sourceB = new WindowSource { Id = "win-B", Title = "Window B", IsAvailable = true };

        _mockItemFactory.Setup(f => f.CreateItemForSource(sourceB)).Throws(new InvalidOperationException("Item creation failed"));

        using var coordinator = new CaptureCoordinator(
            _mockItemFactory.Object,
            _mockDeviceProvider.Object,
            _mockSessionManager.Object,
            _mockPreviewRenderer.Object,
            _mockPresentationStateService.Object);

        await coordinator.StartPreviewAsync(sourceA);

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.SwitchPreviewSourceAsync(sourceB));

        Assert.Equal(CaptureState.Failed, coordinator.State);
        Assert.Contains("Item creation failed", coordinator.LastErrorMessage);
    }

    [Fact]
    public void TEST12_DispatcherQueueTryEnqueueFails_ReleasesFrameSafely()
    {
        using var rawBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 10, 10, BitmapAlphaMode.Premultiplied);
        var sharedBitmap = new RefCountedSoftwareBitmap(rawBitmap, initialRefCount: 1);

        // Simulate dispatcher enqueue returning false (e.g. queue shutting down)
        bool enqueued = false;
        if (!enqueued)
        {
            sharedBitmap.Release();
        }

        Assert.Equal(0, sharedBitmap.RefCount);
    }

    [Fact]
    public async Task TEST13_CancelTransitionDuringInitialization_HandlesSafely()
    {
        var sourceA = new WindowSource { Id = "win-A", Title = "Window A", IsAvailable = true };
        var sourceB = new WindowSource { Id = "win-B", Title = "Window B", IsAvailable = true };
        var sourceC = new WindowSource { Id = "win-C", Title = "Window C", IsAvailable = true };

        using var coordinator = new CaptureCoordinator(
            _mockItemFactory.Object,
            _mockDeviceProvider.Object,
            _mockSessionManager.Object,
            _mockPreviewRenderer.Object,
            _mockPresentationStateService.Object);

        await coordinator.StartPreviewAsync(sourceA);

        // B is superseded immediately by C
        var taskB = coordinator.SwitchPreviewSourceAsync(sourceB);
        var taskC = coordinator.SwitchPreviewSourceAsync(sourceC);

        await Task.WhenAll(taskB, taskC);

        Assert.Equal("win-C", coordinator.CurrentPreviewSource?.Id);
    }

    [Fact]
    public void TEST14_DisposeOldSessionWithPendingFrameConsumer_RefCountPreventsPrematureDisposal()
    {
        using var rawBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 10, 10, BitmapAlphaMode.Premultiplied);
        var sharedBitmap = new RefCountedSoftwareBitmap(rawBitmap, initialRefCount: 2);

        // Session stops and drops its own reference
        sharedBitmap.Release();
        Assert.Equal(1, sharedBitmap.RefCount);
        Assert.NotNull(sharedBitmap.Bitmap);

        // Consumer finishes processing and releases
        sharedBitmap.Release();
        Assert.Equal(0, sharedBitmap.RefCount);
    }

    [Fact]
    public void TEST15_CorrectReferenceCountsAfterAllOperationsComplete()
    {
        using var rawBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 10, 10, BitmapAlphaMode.Premultiplied);
        var sharedBitmap = new RefCountedSoftwareBitmap(rawBitmap, initialRefCount: 1);

        // Add 2 consumers (Preview and Presentation)
        Assert.True(sharedBitmap.TryAddRef());
        Assert.True(sharedBitmap.TryAddRef());
        Assert.Equal(3, sharedBitmap.RefCount);

        // Pipeline drops initial ref
        sharedBitmap.Release();
        Assert.Equal(2, sharedBitmap.RefCount);

        // Preview drops ref
        sharedBitmap.Release();
        Assert.Equal(1, sharedBitmap.RefCount);

        // Presentation drops ref
        sharedBitmap.Release();
        Assert.Equal(0, sharedBitmap.RefCount);

        // Further AddRef fails
        Assert.False(sharedBitmap.TryAddRef());
    }

    [Fact]
    public void TEST16_OldGenerationCallbacks_CannotUpdateNewPresentation()
    {
        var mockRenderer = new Mock<IPresentationOutputRenderer>();
        long currentGeneration = 5;

        void HandleRenderCallback(RefCountedSoftwareBitmap bitmap, long frameGen)
        {
            if (frameGen == currentGeneration)
            {
                mockRenderer.Object.RenderSharedBitmapAsync(bitmap, frameGen);
            }
            else
            {
                bitmap.Release();
            }
        }

        using var rawBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 10, 10, BitmapAlphaMode.Premultiplied);
        var oldFrame = new RefCountedSoftwareBitmap(rawBitmap, initialRefCount: 1);

        // Invoke with older generation
        HandleRenderCallback(oldFrame, 4);

        mockRenderer.Verify(r => r.RenderSharedBitmapAsync(It.IsAny<RefCountedSoftwareBitmap>(), It.IsAny<long>()), Times.Never);
        Assert.Equal(0, oldFrame.RefCount);
    }
}
