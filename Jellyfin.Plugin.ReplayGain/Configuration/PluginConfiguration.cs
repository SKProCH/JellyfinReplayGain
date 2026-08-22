using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ReplayGain.Configuration;

public sealed class PluginConfiguration : BasePluginConfiguration
{
    public bool Enabled { get; set; } = false;

    public bool UseLoudnorm { get; set; } = false;

    public double LoudnormIntegratedLoudness { get; set; } = -16.0;

    public double LoudnormTruePeak { get; set; } = -1.5;

    public double LoudnormLoudnessRange { get; set; } = 11.0;
}