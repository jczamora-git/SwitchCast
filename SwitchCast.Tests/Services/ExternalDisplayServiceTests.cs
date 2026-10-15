using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using Xunit;

namespace SwitchCast.Tests.Services;

public class ExternalDisplayServiceTests
{
    private readonly Mock<IMonitorDiscoveryService> _mockMonitorService;
    private readonly Mock<IPresentationWindowService> _mockWindowService;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;
    private readonly UserSettings _settings;

    public ExternalDisplayServiceTests()
    {
        _mockMonitorService = new Mock<IMonitorDiscoveryService>();
        _mockWindowService = new Mock<IPresentationWindowService>();
        _mockSettingsService = new Mock<IApplicationSettingsService>();
        _settings = new UserSettings
        {
            AutoDetectExternalDisplay = true,
            AutoFullscreenOnExternalDisplay = true,
            PreferredDisplayDeviceName = null
        };
        _mockSettingsService.Setup(s => s.CurrentSettings).Returns(_settings);
        _mockWindowService.Setup(w => w.IsWindowOpen).Returns(true);
    }

    private static MonitorSource CreateMonitor(string id, string deviceName, int left, int top, int width, int height, bool isPrimary)
    {
        return new MonitorSource
        {
            Id = id,
            Title = $"Display {deviceName}",
            DeviceName = deviceName,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            WorkAreaLeft = left,
            WorkAreaTop = top,
            WorkAreaWidth = width,
            WorkAreaHeight = height,
            IsPrimary = isPrimary,
            IsAvailable = true,
            MonitorHandle = new nint(Math.Abs(id.GetHashCode()) + 1)
        };
    }

    [Fact]
    public async Task SingleMonitor_ReturnsNoEligibleDisplays_AndDoesNotRelocate()
    {
        var primary = CreateMonitor("mon_1", @"\\.\DISPLAY1", 0, 0, 1920, 1080, true);
        _mockMonitorService.Setup(m => m.EnumerateMonitorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { primary });

        var service = new ExternalDisplayService(_mockMonitorService.Object, _mockWindowService.Object, _mockSettingsService.Object);

        var eligible = await service.GetEligibleExternalDisplaysAsync();
        var selected = await service.SelectPresentationDisplayAsync();
        var applied = await service.ApplyDisplayAutomationAsync();

        Assert.Empty(eligible);
        Assert.Null(selected);
        Assert.False(applied);
        _mockWindowService.Verify(w => w.MoveToMonitor(It.IsAny<MonitorSource>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task MirroredMonitors_DuplicateGeometry_ReturnsNoEligibleDisplays()
    {
        // Mirrored displays share identical virtual desktop geometry
        var primary = CreateMonitor("mon_1", @"\\.\DISPLAY1", 0, 0, 1920, 1080, true);
        var mirrored = CreateMonitor("mon_2", @"\\.\DISPLAY2", 0, 0, 1920, 1080, false);
        _mockMonitorService.Setup(m => m.EnumerateMonitorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { primary, mirrored });

        var service = new ExternalDisplayService(_mockMonitorService.Object, _mockWindowService.Object, _mockSettingsService.Object);

        var eligible = await service.GetEligibleExternalDisplaysAsync();
        var selected = await service.SelectPresentationDisplayAsync();
        var applied = await service.ApplyDisplayAutomationAsync();

        Assert.Empty(eligible);
        Assert.Null(selected);
        Assert.False(applied);
        _mockWindowService.Verify(w => w.MoveToMonitor(It.IsAny<MonitorSource>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task TwoExtendedMonitors_SelectsSecondary_MovesAndEntersFullscreen()
    {
        var primary = CreateMonitor("mon_1", @"\\.\DISPLAY1", 0, 0, 1920, 1080, true);
        var secondary = CreateMonitor("mon_2", @"\\.\DISPLAY2", 1920, 0, 1920, 1080, false);
        _mockMonitorService.Setup(m => m.EnumerateMonitorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { primary, secondary });

        var service = new ExternalDisplayService(_mockMonitorService.Object, _mockWindowService.Object, _mockSettingsService.Object);

        var eligible = await service.GetEligibleExternalDisplaysAsync();
        var selected = await service.SelectPresentationDisplayAsync();
        var applied = await service.ApplyDisplayAutomationAsync();

        Assert.Single(eligible);
        Assert.Equal(secondary.Id, eligible[0].Id);
        Assert.NotNull(selected);
        Assert.Equal(secondary.Id, selected.Id);
        Assert.True(applied);
        Assert.Equal(secondary.Id, service.CurrentAssignedDisplay?.Id);

        _mockWindowService.Verify(w => w.MoveToMonitor(secondary, true), Times.Once);
    }

    [Fact]
    public async Task TwoExtendedMonitors_AutoFullscreenDisabled_MovesWithoutFullscreen()
    {
        _settings.AutoFullscreenOnExternalDisplay = false;

        var primary = CreateMonitor("mon_1", @"\\.\DISPLAY1", 0, 0, 1920, 1080, true);
        var secondary = CreateMonitor("mon_2", @"\\.\DISPLAY2", 1920, 0, 1920, 1080, false);
        _mockMonitorService.Setup(m => m.EnumerateMonitorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { primary, secondary });

        var service = new ExternalDisplayService(_mockMonitorService.Object, _mockWindowService.Object, _mockSettingsService.Object);

        var applied = await service.ApplyDisplayAutomationAsync();

        Assert.True(applied);
        _mockWindowService.Verify(w => w.MoveToMonitor(secondary, false), Times.Once);
    }

    [Fact]
    public async Task AutoDetectDisabled_DoesNotMoveWindow()
    {
        _settings.AutoDetectExternalDisplay = false;

        var primary = CreateMonitor("mon_1", @"\\.\DISPLAY1", 0, 0, 1920, 1080, true);
        var secondary = CreateMonitor("mon_2", @"\\.\DISPLAY2", 1920, 0, 1920, 1080, false);
        _mockMonitorService.Setup(m => m.EnumerateMonitorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { primary, secondary });

        var service = new ExternalDisplayService(_mockMonitorService.Object, _mockWindowService.Object, _mockSettingsService.Object);

        var applied = await service.ApplyDisplayAutomationAsync();

        Assert.False(applied);
        _mockWindowService.Verify(w => w.MoveToMonitor(It.IsAny<MonitorSource>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ThreeExtendedMonitors_SelectsPreferredDisplay()
    {
        _settings.PreferredDisplayDeviceName = @"\\.\DISPLAY3";

        var primary = CreateMonitor("mon_1", @"\\.\DISPLAY1", 0, 0, 1920, 1080, true);
        var mon2 = CreateMonitor("mon_2", @"\\.\DISPLAY2", 1920, 0, 1920, 1080, false);
        var mon3 = CreateMonitor("mon_3", @"\\.\DISPLAY3", 3840, 0, 1920, 1080, false);
        _mockMonitorService.Setup(m => m.EnumerateMonitorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { primary, mon2, mon3 });

        var service = new ExternalDisplayService(_mockMonitorService.Object, _mockWindowService.Object, _mockSettingsService.Object);

        var selected = await service.SelectPresentationDisplayAsync();

        Assert.NotNull(selected);
        Assert.Equal(mon3.Id, selected.Id);
    }

    [Fact]
    public async Task ThreeExtendedMonitors_MissingPreferredDisplay_FallsBackToFirstEligibleSafely()
    {
        _settings.PreferredDisplayDeviceName = @"\\.\MISSING_PROJECTOR";

        var primary = CreateMonitor("mon_1", @"\\.\DISPLAY1", 0, 0, 1920, 1080, true);
        var mon2 = CreateMonitor("mon_2", @"\\.\DISPLAY2", 1920, 0, 1920, 1080, false);
        var mon3 = CreateMonitor("mon_3", @"\\.\DISPLAY3", 3840, 0, 1920, 1080, false);
        _mockMonitorService.Setup(m => m.EnumerateMonitorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { primary, mon2, mon3 });

        var service = new ExternalDisplayService(_mockMonitorService.Object, _mockWindowService.Object, _mockSettingsService.Object);

        var selected = await service.SelectPresentationDisplayAsync();

        Assert.NotNull(selected);
        Assert.Equal(mon2.Id, selected.Id);
    }

    [Fact]
    public async Task ExternalDisplayDisconnect_RestoresToPrimaryWindowed_NeverFullscreenOnLaptop()
    {
        var primary = CreateMonitor("mon_1", @"\\.\DISPLAY1", 0, 0, 1920, 1080, true);
        var secondary = CreateMonitor("mon_2", @"\\.\DISPLAY2", 1920, 0, 1920, 1080, false);

        // Initially 2 monitors
        _mockMonitorService.Setup(m => m.EnumerateMonitorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { primary, secondary });

        var service = new ExternalDisplayService(_mockMonitorService.Object, _mockWindowService.Object, _mockSettingsService.Object);
        await service.ApplyDisplayAutomationAsync();

        Assert.Equal(secondary.Id, service.CurrentAssignedDisplay?.Id);

        // Disconnect external monitor -> only primary remains
        _mockMonitorService.Setup(m => m.EnumerateMonitorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { primary });

        await service.HandleDisplayTopologyChangedAsync();

        Assert.Null(service.CurrentAssignedDisplay);
        // Verify output restored to primary in WINDOWED mode
        _mockWindowService.Verify(w => w.RestoreToPrimaryMonitor(true), Times.Once);
    }
}
