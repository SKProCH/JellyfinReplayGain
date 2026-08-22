using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ReplayGain.Configuration;

public sealed class PluginConfiguration : BasePluginConfiguration
{
    public bool Enabled { get; set; } = false;
}
