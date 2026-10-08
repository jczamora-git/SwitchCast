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
}
