using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lattice;

/// <summary>Registers the Flare host services of Lattice.</summary>
public static class LatticeComponentsServiceCollectionExtensions
{
    /// <summary>Adds the host services; the application still registers Flare and Lattice.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddLatticeComponents(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ILatticeIconResolver, NullLatticeIconResolver>();
        return services;
    }
}
