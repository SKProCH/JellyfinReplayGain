using Jellyfin.Plugin.ReplayGain.Loudness;
using Jellyfin.Plugin.ReplayGain.Loudnorm;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ReplayGain;

public sealed class PluginServiceRegistrator : IPluginServiceRegistrator {
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost) {
        serviceCollection.AddSingleton<LoudnessCacheStore>();
        serviceCollection.AddSingleton<LoudnormAnalyzer>();
        serviceCollection.AddSingleton<IScheduledTask>(serviceProvider =>
            serviceProvider.GetRequiredService<LoudnormAnalyzer>());
        serviceCollection.AddSingleton<ILibraryPostScanTask, LoudnormPostScanTask>();
        serviceCollection.AddSingleton<ReplayGainPlaybackInfoFilter>();
        serviceCollection.Configure<MvcOptions>(options => options.Filters.Add<ReplayGainPlaybackInfoFilter>());
        if (serviceCollection.Any(descriptor => descriptor.ServiceType == typeof(ITranscodeManager))) {
            serviceCollection.Decorate<ITranscodeManager, ReplayGainTranscodeManager>();
        }
    }
}
