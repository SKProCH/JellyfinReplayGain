using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ReplayGain.Loudnorm;

public sealed class LoudnormPostScanTask : ILibraryPostScanTask {
    private readonly ITaskManager _taskManager;
    private readonly ILogger<LoudnormPostScanTask> _logger;

    public LoudnormPostScanTask(ITaskManager taskManager, ILogger<LoudnormPostScanTask> logger) {
        _taskManager = taskManager;
        _logger = logger;
    }

    public Task Run(IProgress<double> progress, CancellationToken cancellationToken) {
        if (ReplayGainPlugin.IsEnabled) {
            _logger.LogInformation("Queueing ReplayGain loudnorm analysis after library scan");
            _taskManager.QueueIfNotRunning<LoudnormAnalyzer>();
        }
        else {
            _logger.LogDebug("ReplayGain loudnorm analysis was not queued because the plugin is disabled");
        }

        return Task.CompletedTask;
    }
}
