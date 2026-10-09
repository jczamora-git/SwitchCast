using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.Services;

public class PresentationFullscreenTests
{
    [Fact]
    public void PresenterDockViewModel_FullscreenProperties_SynchronizeWithWindowService()
    {
        var mockCoordinator = new Mock<IPresentationCoordinator>();
        var mockStateService = new Mock<IPresentationStateService>();
        var mockWindowService = new Mock<IPresentationWindowService>();
        var mockDockService = new Mock<IPresenterDockService>();
        var mockSettingsService = new Mock<IApplicationSettingsService>();

        mockSettingsService.SetupGet(s => s.CurrentSettings).Returns(new UserSettings());
        mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource>());
        mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.LiveOnly);

        // 1. Output window closed
        mockWindowService.SetupGet(w => w.IsWindowOpen).Returns(false);
        mockWindowService.SetupGet(w => w.DisplayMode).Returns(PresentationDisplayMode.Windowed);

        var vm = new PresenterDockViewModel(
            mockCoordinator.Object,
            mockStateService.Object,
            mockWindowService.Object,
            mockDockService.Object,
            mockSettingsService.Object);

        Assert.False(vm.IsOutputWindowOpen);
        Assert.False(vm.IsFullscreen);
        Assert.False(vm.CanToggleFullscreen);
        Assert.Equal("\uE740", vm.FullscreenButtonGlyph); // Enter Fullscreen icon
        Assert.Equal("Enter Fullscreen Presentation", vm.FullscreenButtonTooltip);

        // 2. Output window opened in Windowed mode
        mockWindowService.SetupGet(w => w.IsWindowOpen).Returns(true);
        mockWindowService.SetupGet(w => w.DisplayMode).Returns(PresentationDisplayMode.Windowed);
        mockWindowService.Raise(w => w.WindowOpened += null, EventArgs.Empty);

        Assert.True(vm.IsOutputWindowOpen);
        Assert.True(vm.CanToggleFullscreen);
        Assert.False(vm.IsFullscreen);

        // Toggle fullscreen command
        vm.ToggleFullscreenCommand.Execute(null);
        mockWindowService.Verify(w => w.ToggleDisplayMode(), Times.Once);

        // 3. Output window in Fullscreen mode
        mockWindowService.SetupGet(w => w.DisplayMode).Returns(PresentationDisplayMode.Fullscreen);
        mockWindowService.Raise(w => w.DisplayModeChanged += null, mockWindowService.Object, PresentationDisplayMode.Fullscreen);

        Assert.True(vm.IsFullscreen);
        Assert.Equal("\uE73F", vm.FullscreenButtonGlyph); // Exit Fullscreen icon
        Assert.Equal("Exit Fullscreen Presentation", vm.FullscreenButtonTooltip);

        // Toggle exit fullscreen
        vm.ToggleFullscreenCommand.Execute(null);
        mockWindowService.Verify(w => w.ToggleDisplayMode(), Times.Exactly(2));

        // 4. Output window closed
        mockWindowService.SetupGet(w => w.IsWindowOpen).Returns(false);
        mockWindowService.SetupGet(w => w.DisplayMode).Returns(PresentationDisplayMode.Windowed);
        mockWindowService.Raise(w => w.WindowClosed += null, EventArgs.Empty);

        Assert.False(vm.IsOutputWindowOpen);
        Assert.False(vm.CanToggleFullscreen);
        Assert.False(vm.IsFullscreen);
    }
}
