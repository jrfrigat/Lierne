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

        foreach (var descriptor in services)
        {
            if (!descriptor.IsKeyedService && descriptor.ImplementationInstance is CatalogValidation validation)
            {
                RunValidation(catalog, validation.Validate);
            }
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
        services.AddScoped<LatticeStatePersistence>();
        return services;
    }

    /// <summary>Adds an application-specific composition check for an existing or future catalog.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="validate">The check to perform at registration time.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddLatticeValidation(
        this IServiceCollection services,
        Func<LatticePluginCatalog, IEnumerable<LatticeDiagnostic>> validate)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(validate);
        if (services.LastOrDefault(descriptor =>
                descriptor.ServiceType == typeof(LatticePluginCatalog) && !descriptor.IsKeyedService)
            ?.ImplementationInstance is LatticePluginCatalog catalog)
        {
            RunValidation(catalog, validate);
        }
        else if (!services.Any(descriptor =>
                     descriptor.ServiceType == typeof(CatalogValidation) && !descriptor.IsKeyedService &&
                     descriptor.ImplementationInstance is CatalogValidation existing && existing.Validate == validate))
        {
            services.AddSingleton(new CatalogValidation(validate));
        }

        return services;
    }

    private static void RunValidation(
        LatticePluginCatalog catalog,
        Func<LatticePluginCatalog, IEnumerable<LatticeDiagnostic>> validate)
    {
        var diagnostics = validate(catalog).ToArray();
        if (diagnostics.Any(diagnostic => diagnostic.Severity == LatticeDiagnosticSeverity.Error))
        {
            throw new LatticeCompositionException(diagnostics);
        }
    }

    private sealed record CatalogValidation(Func<LatticePluginCatalog, IEnumerable<LatticeDiagnostic>> Validate);
}
