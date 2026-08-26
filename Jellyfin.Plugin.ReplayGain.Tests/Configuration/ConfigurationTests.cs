using Jellyfin.Plugin.ReplayGain.Configuration;

namespace Jellyfin.Plugin.ReplayGain.Tests.Configuration;

public sealed class ConfigurationTests {
    [Fact]
    public void PluginConfiguration_HasSafeDefaults() {
        var configuration = new PluginConfiguration();

        configuration.Enabled.Should().BeTrue();
        configuration.MeasurementMethod.Should().Be(MeasurementMethod.Ebur128);
        configuration.PreserveDynamicRange.Should().BeFalse();
        configuration.LoudnormIntegratedLoudness.Should().Be(-16.0);
        configuration.LoudnormTruePeak.Should().Be(-1.5);
        configuration.LoudnormLoudnessRange.Should().Be(11.0);
    }
}
