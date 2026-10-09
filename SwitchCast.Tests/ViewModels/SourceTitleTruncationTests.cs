using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.Services.Capture;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.ViewModels;

public class SourceTitleTruncationTests
{
    private readonly Mock<IPresentationStateService> _mockStateService;
    private readonly Mock<INavigationService> _mockNavigationService;
    private readonly Mock<ICaptureCoordinator> _mockCaptureCoordinator;
    private readonly Mock<IPresentationCoordinator> _mockPresentationCoordinator;
    private readonly Mock<IPresentationWindowService> _mockPresentationWindowService;
    private readonly Mock<IPresenterDockService> _mockDockService;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;

    public SourceTitleTruncationTests()
    {
        _mockStateService = new Mock<IPresentationStateService>();
        _mockNavigationService = new Mock<INavigationService>();
        _mockCaptureCoordinator = new Mock<ICaptureCoordinator>();
        _mockPresentationCoordinator = new Mock<IPresentationCoordinator>();
        _mockPresentationWindowService = new Mock<IPresentationWindowService>();
        _mockDockService = new Mock<IPresenterDockService>();
        _mockSettingsService = new Mock<IApplicationSettingsService>();

        _mockSettingsService.SetupGet(s => s.CurrentSettings).Returns(new UserSettings());
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Idle);
        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.ActiveAndLive);
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource>());
        _mockStateService.SetupGet(s => s.SelectedSourceCount).Returns(0);
    }

    [Fact]
    public void CaptureSource_PreservesFullTitle_WhenTitleIsExtremelyLong()
    {
        const string veryLongTitle = "John Christopher King Zamora — Full-Stack Developer & Graphic Designer - Portfolio & Case Studies - Google Chrome - Profile 1 - Personal";
        var windowSource = new WindowSource
        {
            Id = "win-long-1",
            Title = veryLongTitle,
            ProcessName = "chrome.exe",
            ProcessId = 12345,
            WindowHandle = 0x1A2B3C
        };

        Assert.Equal(veryLongTitle, windowSource.Title);
        Assert.Equal("win-long-1", windowSource.Id);
    }

    [Fact]
    public void CaptureSource_PreservesFullTitle_WithUnicodeAndSpecialCharacters()
    {
        const string unicodeTitle = "🚀 Project Launch 2026 — プレゼンテーション • Final Version [Protected] — Adobe Acrobat";
        var windowSource = new WindowSource
        {
            Id = "win-unicode-1",
            Title = unicodeTitle,
            ProcessName = "AcroRd32.exe",
            ProcessId = 9999,
            WindowHandle = 0x5E6F
        };

        Assert.Equal(unicodeTitle, windowSource.Title);
        Assert.Equal("win-unicode-1", windowSource.Id);
    }

    [Fact]
    public void SelectableSourceItem_ExposesFullTitleAndFormattedSubtitle()
    {
        const string fullTitle = "John Christopher King Zamora — Full-Stack Developer & Graphic Designer - Google Chrome";
        var windowSource = new WindowSource
        {
            Id = "win-item-1",
            Title = fullTitle,
            ProcessName = "chrome.exe",
            ProcessId = 8888,
            WindowHandle = 0x7777
        };

        var item = new SelectableSourceItem(windowSource);

        Assert.Equal(fullTitle, item.Title);
        Assert.Equal("Process: chrome.exe (PID: 8888)", item.Subtitle);
    }

    [Fact]
    public void DashboardViewModel_ActiveSourceTitle_ReturnsFullUnderlyingTitle()
    {
        const string longTitle = "John Christopher King Zamora — Full-Stack Developer & Graphic Designer - Google Chrome";
        var source = new WindowSource
        {
            Id = "win-dash-1",
            Title = longTitle,
            ProcessName = "chrome.exe",
            ProcessId = 1111,
            WindowHandle = 0x2222
        };

        _mockPresentationCoordinator.SetupGet(p => p.CurrentPresentationSource).Returns(source);

        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object);

        Assert.Equal(longTitle, vm.ActiveSourceTitle);
    }

    [Fact]
    public void PresenterDockViewModel_TooltipContainsCompleteLongTitle()
    {
        const string longTitle = "Very Long Window Title That Spans Across Multiple Display Columns And Needs Ellipsis Trimming In The Dock";
        var source = new WindowSource
        {
            Id = "win-dock-1",
            Title = longTitle,
            ProcessName = "app.exe",
            ProcessId = 3333,
            WindowHandle = 0x4444
        };

        _mockStateService.SetupGet(s => s.ActiveSource).Returns(source);
        _mockStateService.SetupGet(s => s.SelectedSource).Returns(source);
        _mockPresentationCoordinator.SetupGet(p => p.CurrentPresentationSource).Returns(source);

        var vm = new PresenterDockViewModel(
            _mockPresentationCoordinator.Object,
            _mockStateService.Object,
            _mockPresentationWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        Assert.Equal(longTitle, vm.ActiveSourceTitle);
        Assert.Contains(longTitle, vm.SourceFullTooltip);
    }

    [Fact]
    public void SourceSwitching_PreservesIdentity_WithLongTitles()
    {
        const string longTitle1 = "Source Alpha — Extremely Long Descriptive Name That Might Exceed Layout Bounds";
        const string longTitle2 = "Source Beta — Another Substantially Long Presentation Surface Window Title";

        var source1 = new WindowSource { Id = "win-alpha", Title = longTitle1, IsAvailable = true };
        var source2 = new WindowSource { Id = "win-beta", Title = longTitle2, IsAvailable = true };

        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource> { source1, source2 });
        _mockStateService.SetupGet(s => s.SelectedSourceCount).Returns(2);

        var vm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            _mockPresentationCoordinator.Object);

        vm.SelectedPresentationSource = source2;

        Assert.Equal("win-beta", vm.SelectedPresentationSource.Id);
        Assert.Equal(longTitle2, vm.SelectedPresentationSource.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Short")]
    [InlineData("Medium Length Window Title — App")]
    [InlineData("Very Long Title — John Christopher King Zamora — Full-Stack Developer & Graphic Designer - Google Chrome")]
    [InlineData("🚀 Unicode — プレゼンテーション 2026")]
    public void CaptureSource_HandlesVariousTitleLengthsAndFormats(string title)
    {
        var source = new WindowSource
        {
            Id = $"win-{title.GetHashCode()}",
            Title = title,
            ProcessName = "test.exe",
            ProcessId = 100,
            WindowHandle = 0x100
        };

        Assert.Equal(title, source.Title);
        var item = new SelectableSourceItem(source);
        Assert.Equal(title, item.Title);
    }
}
