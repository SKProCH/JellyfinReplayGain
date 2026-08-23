using Jellyfin.Plugin.ReplayGain.Configuration;

namespace Jellyfin.Plugin.ReplayGain.Tests.Configuration;

public sealed class ConfigurationTests {
    [Fact]
    public void PluginConfiguration_HasSafeDefaults() {
        new PluginConfiguration().Enabled.Should().BeTrue();
    }
}