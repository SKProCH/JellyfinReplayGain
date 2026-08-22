using System.Reflection;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ReplayGain;

public sealed class ReplayGainPlugin : BasePlugin<Configuration.PluginConfiguration>, IHasWebPages
{
    private readonly ILogger<ReplayGainPlugin> _logger;

    public ReplayGainPlugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILogger<ReplayGainPlugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        _logger = logger;
        Instance = this;
    }

    public static ReplayGainPlugin? Instance { get; private set; }

    public override string Name => "ReplayGain";

    public override string Description => "Normalizes transcoded audio using FFmpeg ReplayGain track metadata.";

    public override Guid Id => Guid.Parse("7a0dc2b9-5e9b-4f4f-8e68-1d0a2b7e4c91");

    public override string ConfigurationFileName => Path.ChangeExtension(AssemblyFileName, ".xml");

    public IEnumerable<PluginPageInfo> GetPages() =>
    [
        new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = GetType().Namespace + ".Web.configurationPage.html"
        }
    ];

    public static bool IsEnabled => Instance?.Configuration.Enabled == true;

    public override void SaveConfiguration(Configuration.PluginConfiguration config)
    {
        try
        {
            base.SaveConfiguration(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save ReplayGain configuration; ReplayGain remains disabled until it can be saved");
        }
    }
}
