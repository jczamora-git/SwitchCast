using Moq;
using SwitchCast.Models;
using SwitchCast.Models.Performance;
using SwitchCast.Services;
using SwitchCast.Services.Capture;
using SwitchCast.Services.Performance;
using Xunit;

namespace SwitchCast.Tests.Services;

public class Direct3D11DeviceProviderTests
{
    [Fact]
    public void DefaultRenderingMode_IsAuto()
    {
        var provider = new Direct3D11DeviceProvider();

        Assert.Equal(RenderingMode.Auto, provider.ActiveMode);
    }

    [Fact]
    public void RenderingMode_FollowsSettingsService()
    {
        var mockSettings = new Mock<IApplicationSettingsService>();
        mockSettings.SetupGet(s => s.CurrentSettings).Returns(new UserSettings
        {
            RenderingMode = RenderingMode.CompatibilityMode
        });

        var provider = new Direct3D11DeviceProvider(mockSettings.Object);

        Assert.Equal(RenderingMode.CompatibilityMode, provider.ActiveMode);
    }

    [Fact]
    public void SetRenderingMode_UpdatesActiveMode()
    {
        var provider = new Direct3D11DeviceProvider();

        provider.SetRenderingMode(RenderingMode.PreferHardware);
        Assert.Equal(RenderingMode.PreferHardware, provider.ActiveMode);

        provider.SetRenderingMode(RenderingMode.CompatibilityMode);
        Assert.Equal(RenderingMode.CompatibilityMode, provider.ActiveMode);
    }

    [Fact]
    public void ResetDevice_NotifiesMetricsService()
    {
        var mockMetrics = new Mock<IPerformanceMetricsService>();
        var provider = new Direct3D11DeviceProvider(metricsService: mockMetrics.Object);

        provider.ResetDevice();

        mockMetrics.Verify(m => m.RecordDeviceReset(), Times.Once);
    }

    [Fact]
    public void CompatibilityMode_InitializesWarpRasterizer()
    {
        var provider = new Direct3D11DeviceProvider();
        provider.SetRenderingMode(RenderingMode.CompatibilityMode);

        provider.EnsureDevice();

        Assert.NotNull(provider.Device);
        Assert.NotEqual(nint.Zero, provider.NativeDevicePointer);
        Assert.False(provider.IsHardwareAccelerated);
        Assert.Contains("WARP", provider.ActiveBackendDescription);
    }

    [Fact]
    public void AutoMode_InitializesValidDevice()
    {
        var provider = new Direct3D11DeviceProvider();
        provider.SetRenderingMode(RenderingMode.Auto);

        provider.EnsureDevice();

        Assert.NotNull(provider.Device);
        Assert.NotEqual(nint.Zero, provider.NativeDevicePointer);
        Assert.NotEmpty(provider.ActiveBackendDescription);
    }
}
