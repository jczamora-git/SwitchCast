using Microsoft.UI.Xaml;
using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.ViewModels;

public class PresentationViewModelTests
{
    private readonly Mock<IPresentationStateService> _mockStateService;
    private readonly Mock<IPresentationCoordinator> _mockPresentationCoordinator;

    public PresentationViewModelTests()
    {
        _mockStateService = new Mock<IPresentationStateService>();
        _mockPresentationCoordinator = new Mock<IPresentationCoordinator>();

        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Idle);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns((CaptureSource?)null);
        _mockPresentationCoordinator.SetupGet(p => p.Status).Returns(PresentationStatus.Idle);
        _mockPresentationCoordinator.SetupGet(p => p.IsLive).Returns(false);
        _mockPresentationCoordinator.SetupGet(p => p.IsPaused).Returns(false);
        _mockPresentationCoordinator.SetupGet(p => p.IsBlackout).Returns(false);
    }

    [Fact]
    public void InitialState_ShowsStandbyScreen()
    {
        var vm = new PresentationViewModel(_mockStateService.Object, _mockPresentationCoordinator.Object);

        Assert.Equal(PresentationStatus.Idle, vm.Status);
        Assert.Equal(Visibility.Visible, vm.StandbyVisibility);
        Assert.Equal(Visibility.Collapsed, vm.LiveContentVisibility);
        Assert.Equal(Visibility.Collapsed, vm.BlackoutVisibility);
        Assert.Equal(Visibility.Collapsed, vm.PausedIndicatorVisibility);
        Assert.Equal("Ready to Present", vm.ActiveSourceTitle);
    }

    [Fact]
    public void ActiveState_WithSource_ShowsLiveContent()
    {
        var source = new WindowSource { Id = "win-1", Title = "Slide Deck", IsAvailable = true };
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(source);
        _mockPresentationCoordinator.SetupGet(p => p.Status).Returns(PresentationStatus.Active);
        _mockPresentationCoordinator.SetupGet(p => p.IsLive).Returns(true);

        var vm = new PresentationViewModel(_mockStateService.Object, _mockPresentationCoordinator.Object);

        Assert.Equal(Visibility.Collapsed, vm.StandbyVisibility);
        Assert.Equal(Visibility.Visible, vm.LiveContentVisibility);
        Assert.Equal(Visibility.Collapsed, vm.BlackoutVisibility);
        Assert.Equal(Visibility.Collapsed, vm.PausedIndicatorVisibility);
        Assert.Equal("Slide Deck", vm.ActiveSourceTitle);
    }

    [Fact]
    public void PausedState_ShowsLiveContentAndPausedIndicator()
    {
        var source = new WindowSource { Id = "win-1", Title = "Slide Deck", IsAvailable = true };
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Paused);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(source);
        _mockPresentationCoordinator.SetupGet(p => p.Status).Returns(PresentationStatus.Paused);
        _mockPresentationCoordinator.SetupGet(p => p.IsPaused).Returns(true);

        var vm = new PresentationViewModel(_mockStateService.Object, _mockPresentationCoordinator.Object);

        Assert.Equal(Visibility.Collapsed, vm.StandbyVisibility);
        Assert.Equal(Visibility.Visible, vm.LiveContentVisibility);
        Assert.Equal(Visibility.Visible, vm.PausedIndicatorVisibility);
        Assert.Equal(Visibility.Collapsed, vm.BlackoutVisibility);
    }

    [Fact]
    public void BlackoutState_ShowsBlackoutOverlay()
    {
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Blackout);
        _mockPresentationCoordinator.SetupGet(p => p.Status).Returns(PresentationStatus.Blackout);
        _mockPresentationCoordinator.SetupGet(p => p.IsBlackout).Returns(true);

        var vm = new PresentationViewModel(_mockStateService.Object, _mockPresentationCoordinator.Object);

        Assert.Equal(Visibility.Visible, vm.BlackoutVisibility);
        Assert.Equal(Visibility.Collapsed, vm.StandbyVisibility);
        Assert.Equal(Visibility.Collapsed, vm.LiveContentVisibility);
    }

    [Fact]
    public void StateService_PropertyChanged_RefreshesViewModelProperties()
    {
        var vm = new PresentationViewModel(_mockStateService.Object, _mockPresentationCoordinator.Object);

        // Update mock state and fire event
        var source = new WindowSource { Id = "win-2", Title = "Browser Tab", IsAvailable = true };
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(source);

        _mockStateService.Raise(s => s.PropertyChanged += null, new System.ComponentModel.PropertyChangedEventArgs(nameof(IPresentationStateService.Status)));

        Assert.Equal(Visibility.Visible, vm.LiveContentVisibility);
        Assert.Equal(Visibility.Collapsed, vm.StandbyVisibility);
        Assert.Equal("Browser Tab", vm.ActiveSourceTitle);
    }

    [Fact]
    public void Coordinator_PropertyChanged_RefreshesVisibilityAndImage()
    {
        var vm = new PresentationViewModel(_mockStateService.Object, _mockPresentationCoordinator.Object);

        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Paused);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(new WindowSource { Id = "win-1", Title = "Slide Deck" });
        _mockPresentationCoordinator.SetupGet(p => p.Status).Returns(PresentationStatus.Paused);
        _mockPresentationCoordinator.SetupGet(p => p.IsPaused).Returns(true);

        _mockPresentationCoordinator.Raise(p => p.PropertyChanged += null, new System.ComponentModel.PropertyChangedEventArgs(nameof(IPresentationCoordinator.IsPaused)));

        Assert.Equal(Visibility.Visible, vm.PausedIndicatorVisibility);
    }
}

