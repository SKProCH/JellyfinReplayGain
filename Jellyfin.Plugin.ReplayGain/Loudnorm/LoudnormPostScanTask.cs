using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.ReplayGain.Loudnorm;

public sealed class LoudnormPostScanTask : ILibraryPostScanTask {
    private readonly ITaskManager _taskManager;

    public LoudnormPostScanTask(ITaskManager taskManager) {
        _taskManager = taskManager;
    }

    public Task Run(IProgress<double> progress, CancellationToken cancellationToken) {
        if (ReplayGainPlugin.IsEnabled && ReplayGainPlugin.Instance!.Configuration.UseLoudnorm) {
            _taskManager.QueueIfNotRunning<LoudnormAnalyzer>();
        }

        return Task.CompletedTask;
    }
}