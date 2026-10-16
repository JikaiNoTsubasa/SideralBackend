using System.Reflection;
using dependancies.SharedClasses;

namespace sideral_api.Classes;

public static class ServiceCollectionEx
{
    /// <summary>
    /// Enregistre automatiquement les classes marquées IScoped / ISingleton des assemblies données,
    /// sous leur propre type et sous chacune de leurs interfaces "métier" (hors marqueurs).
    /// </summary>
    public static IServiceCollection AddMarkedServices(this IServiceCollection services, params Assembly[] assemblies)
    {
        var markers = new[] { typeof(IScoped), typeof(ISingleton) };

        var types = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } && markers.Any(m => m.IsAssignableFrom(t)));

        foreach (var type in types)
        {
            var lifetime = typeof(ISingleton).IsAssignableFrom(type) ? ServiceLifetime.Singleton : ServiceLifetime.Scoped;

            services.Add(new ServiceDescriptor(type, type, lifetime));
            foreach (var itf in type.GetInterfaces().Where(i => !markers.Contains(i)))
            {
                services.Add(new ServiceDescriptor(itf, sp => sp.GetRequiredService(type), lifetime));
            }
        }

        return services;
    }
}
