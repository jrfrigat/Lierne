using Microsoft.Extensions.DependencyInjection;

namespace Lattice;

/// <summary>Registers Lattice and composes the application plugins.</summary>
public static class LatticeServiceCollectionExtensions
{
    /// <summary>Adds Lattice with the plugins listed by <paramref name="configure"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Adds the plugins the application is composed from.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="LatticeCompositionException">
    /// The plugin composition has errors; the exception lists every diagnostic.
    /// </exception>
    public static IServiceCollection AddLattice(
        this IServiceCollection services,
        Action<LatticeOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(LatticePluginCatalog)))
        {
            return services;
        }

        var options = new LatticeOptions();
        configure(options);
        var catalog = LatticePluginCatalog.Build(options.Plugins, services);
        if (catalog.HasErrors)
        {
            throw new LatticeCompositionException(catalog.Diagnostics);
        }

        services.AddSingleton(catalog);
        services.AddScoped<ILatticeContext, LatticeContext>();
        services.AddScoped<ILatticeCommandService, LatticeCommandService>();
        services.AddScoped<ILatticeDocumentService, LatticeDocumentService>();
        services.AddScoped<ILatticeToolWindowService, LatticeToolWindowService>();
        services.AddScoped<ISettingsStore, LatticeSettingsStore>();
        services.AddScoped<ILatticeLocalizer, LatticeLocalizer>();
        services.AddScoped<ILatticeShortcutService, LatticeShortcutService>();
        services.AddScoped<LatticeSettingsTransfer>();
        return services;
    }
}
