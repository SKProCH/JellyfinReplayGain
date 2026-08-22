using MediaBrowser.Controller;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ReplayGain;

public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        if (serviceCollection.Any(descriptor => descriptor.ServiceType == typeof(ITranscodeManager)))
        {
            serviceCollection.Decorate<ITranscodeManager, ReplayGainTranscodeManager>();
        }
    }
}
