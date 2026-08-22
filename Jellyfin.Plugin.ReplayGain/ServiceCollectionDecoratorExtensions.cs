using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ReplayGain;

internal static class ServiceCollectionDecoratorExtensions
{
    private const string DecoratedServiceKeySuffix = "+Decorated";

    public static IServiceCollection Decorate<TService, TDecorator>(this IServiceCollection services)
        where TDecorator : TService
        => services.Decorate(typeof(TService), typeof(TDecorator));

    private static IServiceCollection Decorate(this IServiceCollection services, Type serviceType, Type decoratorType)
    {
        var decorated = false;
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var descriptor = services[i];
            if (descriptor.ServiceKey is string key && key.EndsWith(DecoratedServiceKeySuffix, StringComparison.Ordinal))
            {
                continue;
            }

            if (descriptor.ServiceType != serviceType)
            {
                continue;
            }

            var serviceKey = $"{serviceType.Name}+{Guid.NewGuid():n}{DecoratedServiceKeySuffix}";
            services.Add(CreateKeyedDescriptor(descriptor, serviceKey));
            services[i] = new ServiceDescriptor(
                serviceType,
                CreateDecoratorFactory(serviceType, serviceKey, decoratorType),
                descriptor.Lifetime);
            decorated = true;
        }

        return decorated ? services : throw new InvalidOperationException($"No service of type {serviceType.Name} has been registered.");
    }

    private static ServiceDescriptor CreateKeyedDescriptor(ServiceDescriptor original, object serviceKey)
    {
        if (original.ImplementationInstance is not null)
        {
            return new ServiceDescriptor(original.ServiceType, serviceKey, original.ImplementationInstance);
        }

        if (original.ImplementationFactory is not null)
        {
            return new ServiceDescriptor(original.ServiceType, serviceKey, (sp, _) => original.ImplementationFactory(sp), original.Lifetime);
        }

        return new ServiceDescriptor(original.ServiceType, serviceKey, original.ImplementationType!, original.Lifetime);
    }

    private static Func<IServiceProvider, object> CreateDecoratorFactory(Type serviceType, object serviceKey, Type decoratorType)
        => sp =>
        {
            var inner = ((IKeyedServiceProvider)sp).GetRequiredKeyedService(serviceType, serviceKey);
            return ActivatorUtilities.CreateInstance(sp, decoratorType, inner);
        };
}
