using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.Services.Capture;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.ViewModels;

public class DashboardViewModelTests
{
    private readonly Mock<IPresentationStateService> _mockStateService;
    private readonly Mock<INavigationService> _mockNavigationService;
    private readonly Mock<ICaptureCoordinator> _mockCaptureCoordinator;
    private readonly Mock<IPresentationCoordinator> _mockPresentationCoordinator;

    public DashboardViewModelTests()
    {
        _mockStateService = new Mock<IPresentationStateService>();
        _mockNavigationService = new Mock<INavigationService>();
        _mockCaptureCoordinator = new Mock<ICaptureCoordinator>();
        _mockPresentationCoordinator = new Mock<IPresentationCoordinator>();

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
    }

    [Fact]
    public void InitialProperties_ReflectPresentationAndCaptureState()
    {
        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object);

        Assert.Equal(PresentationStatus.Idle, vm.Status);
        Assert.Equal("Not Started", vm.StatusDisplayText);
        Assert.Equal("None", vm.ActiveSourceTitle);
        Assert.Equal(0, vm.SelectedSourceCount);
        Assert.False(vm.HasSelectedSources);
        Assert.False(vm.HasActiveSource);
        Assert.Equal(CaptureState.Idle, vm.CaptureState);
        Assert.True(vm.IsCaptureIdle);
        Assert.False(vm.IsCapturing);
        Assert.False(vm.IsStartingCapture);
        Assert.False(vm.IsOutputWindowOpen);
        Assert.Equal("Window Closed", vm.PresentationOutputStatusText);
        Assert.Equal("Start Presenting", vm.PresentationButtonText);
    }

    [Fact]
    public async Task StartPreviewCommand_CallsCaptureCoordinatorStartPreview()
    {
        var source = new WindowSource { Id = "win-1", Title = "Chrome", IsAvailable = true };
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource> { source });
        _mockStateService.SetupGet(s => s.SelectedSourceCount).Returns(1);

        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object)
        {
            SelectedPreviewSource = source
        };

        await vm.StartPreviewCommand.ExecuteAsync(null);

        _mockCaptureCoordinator.Verify(c => c.StartPreviewAsync(source), Times.Once);
    }

    [Fact]
    public async Task StopPreviewCommand_CallsCaptureCoordinatorStopPreview()
    {
        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object);

        await vm.StopPreviewCommand.ExecuteAsync(null);

        _mockCaptureCoordinator.Verify(c => c.StopPreviewAsync(), Times.Once);
    }

    [Fact]
    public async Task SwitchPreviewSourceCommand_CallsCaptureCoordinatorSwitchPreview()
    {
        var source1 = new WindowSource { Id = "win-1", Title = "Chrome", IsAvailable = true };
        var source2 = new WindowSource { Id = "win-2", Title = "VS Code", IsAvailable = true };

        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Capturing);

        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object);

        await vm.SwitchPreviewSourceCommand.ExecuteAsync(source2);

        _mockCaptureCoordinator.Verify(c => c.SwitchPreviewSourceAsync(source2), Times.Once);
        Assert.Equal(source2, vm.SelectedPreviewSource);
    }

    [Fact]
    public async Task OpenPresentationWindowCommand_CallsPresentationCoordinator()
    {
        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object);

        await vm.OpenPresentationWindowCommand.ExecuteAsync(null);

        _mockPresentationCoordinator.Verify(p => p.OpenOutputWindowAsync(), Times.Once);
    }

    [Fact]
    public async Task StartPresentationCommand_CallsPresentationCoordinator()
    {
        var source = new WindowSource { Id = "win-1", Title = "Chrome", IsAvailable = true };
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource> { source });
        _mockStateService.SetupGet(s => s.SelectedSourceCount).Returns(1);

        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object)
        {
            SelectedPresentationSource = source
        };

        await vm.StartPresentationCommand.ExecuteAsync(null);

        _mockPresentationCoordinator.Verify(p => p.StartPresentationAsync(source), Times.Once);
    }

    [Fact]
    public async Task TogglePauseCommand_WhenLive_PausesPresentation()
    {
        _mockPresentationCoordinator.SetupGet(p => p.IsLive).Returns(true);
        _mockPresentationCoordinator.SetupGet(p => p.IsPaused).Returns(false);

        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object);

        await vm.TogglePauseCommand.ExecuteAsync(null);

        _mockPresentationCoordinator.Verify(p => p.PausePresentationAsync(), Times.Once);
    }

    [Fact]
    public async Task TogglePauseCommand_WhenPaused_ResumesPresentation()
    {
        _mockPresentationCoordinator.SetupGet(p => p.IsPaused).Returns(true);

        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object);

        await vm.TogglePauseCommand.ExecuteAsync(null);

        _mockPresentationCoordinator.Verify(p => p.ResumePresentationAsync(), Times.Once);
    }

    [Fact]
    public async Task ToggleBlackoutCommand_CallsPresentationCoordinator()
    {
        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object);

        await vm.ToggleBlackoutCommand.ExecuteAsync(null);

        _mockPresentationCoordinator.Verify(p => p.ToggleBlackoutAsync(), Times.Once);
    }

    [Fact]
    public void VisibilityProperties_CalculateCorrectly()
    {
        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object);

        // When Idle with 0 selected sources
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, vm.EmptyWorkspaceVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, vm.ReadyToPreviewVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, vm.LivePreviewVisibility);

        // When Capturing
        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Capturing);
        // Trigger notification
        _mockCaptureCoordinator.Raise(c => c.PropertyChanged += null, new System.ComponentModel.PropertyChangedEventArgs(nameof(ICaptureCoordinator.State)));

        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, vm.LivePreviewVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, vm.EmptyWorkspaceVisibility);
    }

    [Fact]
    public void NavigateToSourcesCommand_CallsNavigationService()
    {
        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object);

        vm.NavigateToSourcesCommand.Execute(null);

        _mockNavigationService.Verify(n => n.NavigateTo(It.Is<Type>(t => t.Name == "SourcesPage"), null), Times.Once);
    }
}
