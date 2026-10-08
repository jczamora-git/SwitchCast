using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using Xunit;

namespace SwitchCast.Tests.Services;

public class HotkeyServiceTests
{
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;

    public HotkeyServiceTests()
    {
        _mockSettingsService = new Mock<IApplicationSettingsService>();
        _mockSettingsService.SetupGet(s => s.CurrentSettings).Returns(new UserSettings());
    }

    [Fact]
    public void DefaultSettings_ContainsStandardHotkeys()
    {
        var settings = new UserSettings();
        Assert.True(settings.EnableGlobalHotkeys);
        Assert.True(settings.AutoOpenPresenterDock);
        Assert.True(settings.DockAlwaysOnTop);
        Assert.False(settings.StartDockInCompactMode);
        Assert.NotEmpty(settings.HotkeyBindings);

        var nextSource = settings.HotkeyBindings.FirstOrDefault(h => h.Action == HotkeyAction.NextSource);
        Assert.NotNull(nextSource);
        Assert.Equal("Ctrl + Shift + Right Arrow", nextSource.DisplayString);

        var prevSource = settings.HotkeyBindings.FirstOrDefault(h => h.Action == HotkeyAction.PreviousSource);
        Assert.NotNull(prevSource);
        Assert.Equal("Ctrl + Shift + Left Arrow", prevSource.DisplayString);

        var togglePause = settings.HotkeyBindings.FirstOrDefault(h => h.Action == HotkeyAction.TogglePause);
        Assert.NotNull(togglePause);
        Assert.Equal("Ctrl + Shift + P", togglePause.DisplayString);
    }

    [Fact]
    public void HotkeyBinding_DisplayString_FormatsCorrectly()
    {
        var binding = new HotkeyBinding
        {
            Action = HotkeyAction.ToggleBlackout,
            Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift,
            VirtualKey = 0x42, // 'B'
            Name = "Blackout"
        };

        Assert.Equal("Ctrl + Shift + Alt + B", binding.DisplayString);
    }

    [Fact]
    public void HotkeyService_Initializes_WithoutExceptions()
    {
        using var service = new Win32HotkeyService(_mockSettingsService.Object);
        Assert.True(service.IsEnabled);
        Assert.NotEmpty(service.CurrentBindings);

        // Safe call to initialize
        service.Initialize();
    }

    [Fact]
    public void HotkeyService_SetEnabled_TogglesState()
    {
        using var service = new Win32HotkeyService(_mockSettingsService.Object);
        Assert.True(service.IsEnabled);

        service.SetEnabled(false);
        Assert.False(service.IsEnabled);

        service.SetEnabled(true);
        Assert.True(service.IsEnabled);
    }

    [Fact]
    public void HotkeyService_ReloadSettings_UpdatesState()
    {
        var customSettings = new UserSettings { EnableGlobalHotkeys = false };
        _mockSettingsService.SetupGet(s => s.CurrentSettings).Returns(customSettings);

        using var service = new Win32HotkeyService(_mockSettingsService.Object);
        service.ReloadSettings();

        Assert.False(service.IsEnabled);
    }

    [Fact]
    public void HotkeyService_UnregisterHotkey_RemovesBinding()
    {
        using var service = new Win32HotkeyService(_mockSettingsService.Object);
        service.UnregisterHotkey(HotkeyAction.NextSource);
        // Should complete safely without exception
    }
}
