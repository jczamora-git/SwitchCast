using SwitchCast.Models.Performance;
using SwitchCast.Services.Performance;
using Xunit;

namespace SwitchCast.Tests.Services;

public class GraphicsCapabilityServiceTests
{
    [Fact]
    public void GetCapabilities_ReturnsValidObject()
    {
        var service = new GraphicsCapabilityService();

        var caps = service.GetCapabilities();

        Assert.NotNull(caps);
        Assert.NotNull(caps.FeatureLevel);
        Assert.NotNull(caps.AvailableAdapters);
        Assert.NotNull(caps.SummaryText);
    }

    [Fact]
    public void GetCapabilities_CachesResult_UnlessRefreshed()
    {
        var service = new GraphicsCapabilityService();

        var first = service.GetCapabilities(forceRefresh: false);
        var second = service.GetCapabilities(forceRefresh: false);

        Assert.Same(first, second);

        var refreshed = service.GetCapabilities(forceRefresh: true);
        Assert.NotNull(refreshed);
        Assert.Equal(first.PrimaryAdapter?.Name, refreshed.PrimaryAdapter?.Name);
    }

    [Fact]
    public void Adapters_Collection_IsPopulated()
    {
        var service = new GraphicsCapabilityService();

        var caps = service.GetCapabilities();

        Assert.NotNull(caps.AvailableAdapters);
        foreach (var adapter in caps.AvailableAdapters)
        {
            Assert.False(string.IsNullOrWhiteSpace(adapter.Name));
        }
    }
}
