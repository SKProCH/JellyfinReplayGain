using Jellyfin.Plugin.ReplayGain.Configuration;

namespace Jellyfin.Plugin.ReplayGain.Tests.Configuration;

public sealed class DynamicRangeConfigurationTests
{
    [Fact]
    public void PluginConfiguration_PreservesDynamicRangeByDefaultIsDisabled()
    {
        new PluginConfiguration().PreserveDynamicRange.Should().BeFalse();
    }
}
