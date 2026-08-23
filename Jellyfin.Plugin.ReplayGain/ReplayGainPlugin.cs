using Jellyfin.Plugin.ReplayGain.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using Jellyfin.Plugin.ReplayGain.Loudnorm;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ReplayGain;

public sealed class ReplayGainPlugin : BasePlugin<PluginConfiguration>, IHasWebPages {
    private readonly ILogger<ReplayGainPlugin> _logger;
    private readonly ITaskManager _taskManager;

    public ReplayGainPlugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILogger<ReplayGainPlugin> logger,
        ITaskManager taskManager)
        : base(applicationPaths, xmlSerializer) {
        _logger = logger;
        _taskManager = taskManager;
        Instance = this;
    }

    public static ReplayGainPlugin? Instance { get; private set; }

    public override string Name {
        get => "ReplayGain";
    }

    public override string Description {
        get => "Normalizes transcoded audio using FFmpeg ReplayGain track metadata.";
    }

    public override Guid Id {
        get => Guid.Parse("7a0dc2b9-5e9b-4f4f-8e68-1d0a2b7e4c91");
    }

    public override string ConfigurationFileName {
        get => Path.ChangeExtension(AssemblyFileName, ".xml");
    }

    public static bool IsEnabled {
        get => Instance?.Configuration.Enabled == true;
    }

    public IEnumerable<PluginPageInfo> GetPages() {
        return [
            new PluginPageInfo {
                Name = Name,
                EmbeddedResourcePath = GetType().Namespace + ".Web.configurationPage.html"
            }
        ];
    }

    public override void SaveConfiguration(PluginConfiguration config) {
        try {
            base.SaveConfiguration(config);
            if (config.Enabled) {
                _taskManager.QueueIfNotRunning<LoudnormAnalyzer>();
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex,
                "Failed to save ReplayGain configuration; ReplayGain remains disabled until it can be saved");
        }
    }
}
