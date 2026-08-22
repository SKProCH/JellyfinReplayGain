using MediaBrowser.Controller;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Jellyfin.Plugin.ReplayGain.Loudnorm;

namespace Jellyfin.Plugin.ReplayGain;

public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<LoudnormCacheStore>();
        serviceCollection.AddSingleton<IScheduledTask, LoudnormAnalyzer>();
        if (serviceCollection.Any(descriptor => descriptor.ServiceType == typeof(ITranscodeManager)))
        {
            serviceCollection.Decorate<ITranscodeManager, ReplayGainTranscodeManager>();
        }
    }
}