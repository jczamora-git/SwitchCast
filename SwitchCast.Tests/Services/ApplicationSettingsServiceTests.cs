using SwitchCast.Models;
using SwitchCast.Services;
using Xunit;

namespace SwitchCast.Tests.Services;

public class ApplicationSettingsServiceTests : IDisposable
{
    private readonly string _tempSettingsPath;

    public ApplicationSettingsServiceTests()
    {
        _tempSettingsPath = Path.Combine(Path.GetTempPath(), $"switchcast_test_settings_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempSettingsPath))
        {
            try { File.Delete(_tempSettingsPath); } catch { }
        }
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenFileMissing_UsesSafeDefaults()
    {
        var service = new ApplicationSettingsService(_tempSettingsPath);

        await service.LoadSettingsAsync();

        Assert.NotNull(service.CurrentSettings);
        Assert.Equal(ApplicationThemeOption.System, service.CurrentSettings.Theme);
        Assert.True(service.CurrentSettings.RememberWindowDimensions);
    }

    [Fact]
    public async Task SaveSettingsAsync_AndLoadSettingsAsync_PersistsThemeAndWindowDimensions()
    {
        var service = new ApplicationSettingsService(_tempSettingsPath);
        service.CurrentSettings.Theme = ApplicationThemeOption.Dark;
        service.CurrentSettings.WindowWidth = 1200;
        service.CurrentSettings.WindowHeight = 850;

        await service.SaveSettingsAsync();

        var reloadedService = new ApplicationSettingsService(_tempSettingsPath);
        await reloadedService.LoadSettingsAsync();

        Assert.Equal(ApplicationThemeOption.Dark, reloadedService.CurrentSettings.Theme);
        Assert.Equal(1200, reloadedService.CurrentSettings.WindowWidth);
        Assert.Equal(850, reloadedService.CurrentSettings.WindowHeight);
    }

    [Fact]
    public async Task SetThemeAsync_UpdatesTheme_FiresThemeChangedEvent_AndPersists()
    {
        var service = new ApplicationSettingsService(_tempSettingsPath);
        ApplicationThemeOption? eventFiredTheme = null;
        service.ThemeChanged += (s, theme) => eventFiredTheme = theme;

        await service.SetThemeAsync(ApplicationThemeOption.Light);

        Assert.Equal(ApplicationThemeOption.Light, service.CurrentSettings.Theme);
        Assert.Equal(ApplicationThemeOption.Light, eventFiredTheme);

        var reloadedService = new ApplicationSettingsService(_tempSettingsPath);
        await reloadedService.LoadSettingsAsync();
        Assert.Equal(ApplicationThemeOption.Light, reloadedService.CurrentSettings.Theme);
    }

    [Fact]
    public async Task SetWindowDimensionsAsync_WhenRememberDimensionsFalse_DoesNotUpdate()
    {
        var service = new ApplicationSettingsService(_tempSettingsPath);
        service.CurrentSettings.RememberWindowDimensions = false;
        service.CurrentSettings.WindowWidth = 1000;
        service.CurrentSettings.WindowHeight = 700;

        await service.SetWindowDimensionsAsync(1400, 900);

        Assert.Equal(1000, service.CurrentSettings.WindowWidth);
        Assert.Equal(700, service.CurrentSettings.WindowHeight);
    }

    [Fact]
    public async Task SetWindowPositionAsync_WhenRememberPositionTrue_PersistsCoordinates()
    {
        var service = new ApplicationSettingsService(_tempSettingsPath);
        service.CurrentSettings.RememberWindowPosition = true;

        await service.SetWindowPositionAsync(250, 150);

        Assert.Equal(250, service.CurrentSettings.WindowPositionX);
        Assert.Equal(150, service.CurrentSettings.WindowPositionY);

        var reloaded = new ApplicationSettingsService(_tempSettingsPath);
        await reloaded.LoadSettingsAsync();
        Assert.Equal(250, reloaded.CurrentSettings.WindowPositionX);
        Assert.Equal(150, reloaded.CurrentSettings.WindowPositionY);
    }

    [Fact]
    public async Task SetWindowPositionAsync_WhenRememberPositionFalse_DoesNotUpdate()
    {
        var service = new ApplicationSettingsService(_tempSettingsPath);
        service.CurrentSettings.RememberWindowPosition = false;
        service.CurrentSettings.WindowPositionX = null;
        service.CurrentSettings.WindowPositionY = null;

        await service.SetWindowPositionAsync(250, 150);

        Assert.Null(service.CurrentSettings.WindowPositionX);
        Assert.Null(service.CurrentSettings.WindowPositionY);
    }

    [Fact]
    public async Task SetWindowPlacementAsync_RespectsFlagsAndPersists()
    {
        var service = new ApplicationSettingsService(_tempSettingsPath);
        service.CurrentSettings.RememberWindowDimensions = true;
        service.CurrentSettings.RememberWindowPosition = true;

        await service.SetWindowPlacementAsync(1280, 800, 300, 200);

        Assert.Equal(1280, service.CurrentSettings.WindowWidth);
        Assert.Equal(800, service.CurrentSettings.WindowHeight);
        Assert.Equal(300, service.CurrentSettings.WindowPositionX);
        Assert.Equal(200, service.CurrentSettings.WindowPositionY);
    }
}
