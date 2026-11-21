using Microsoft.Extensions.DependencyInjection;

namespace Lierne;

/// <summary>Registers Lierne and composes the application plugins.</summary>
public static class LierneServiceCollectionExtensions
{
    /// <summary>Adds Lierne with the plugins listed by <paramref name="configure"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Adds the plugins the application is composed from.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="LierneCompositionException">
    /// The plugin composition has errors; the exception lists every diagnostic.
    /// </exception>
    public static IServiceCollection AddLierne(
        this IServiceCollection services,
        Action<LierneOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(LiernePluginCatalog)))
        {
            return services;
        }

        var options = new LierneOptions();
        configure(options);
        var catalog = LiernePluginCatalog.Build(options.Plugins, services);
        if (catalog.HasErrors)
        {
            throw new LierneCompositionException(catalog.Diagnostics);
        }

        foreach (var descriptor in services)
        {
            if (!descriptor.IsKeyedService && descriptor.ImplementationInstance is CatalogValidation validation)
            {
                RunValidation(catalog, validation.Validate);
            }
        }

        services.AddSingleton(catalog);
        services.AddScoped<ILierneContext, LierneContext>();
        services.AddScoped<LierneCommandService>();
        services.AddScoped<ILierneCommandService>(provider =>
            provider.GetRequiredService<LierneCommandService>());
        services.AddScoped<ICommandPresentationService>(provider =>
            provider.GetRequiredService<LierneCommandService>());
        services.AddScoped<ICommandPlacementService, LierneCommandPlacementService>();
        services.AddScoped<ILierneDocumentService, LierneDocumentService>();
        services.AddScoped<ILierneToolWindowService, LierneToolWindowService>();
        services.AddScoped<ISettingsStore, LierneSettingsStore>();
        services.AddScoped<ILierneLocalizer, LierneLocalizer>();
        services.AddScoped<ILierneShortcutService, LierneShortcutService>();
        services.AddScoped<ILierneReadinessService, LierneReadinessService>();
        services.AddScoped<ILierneProgressService, LierneProgressService>();
        services.AddScoped<ILierneEventBus, LierneEventBus>();
        services.AddScoped<LierneSettingsTransfer>();
        services.AddScoped<LierneStatePersistence>();
        return services;
    }

    /// <summary>Adds an application-specific composition check for an existing or future catalog.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="validate">The check to perform at registration time.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddLierneValidation(
        this IServiceCollection services,
        Func<LiernePluginCatalog, IEnumerable<LierneDiagnostic>> validate)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(validate);
        if (services.LastOrDefault(descriptor =>
                descriptor.ServiceType == typeof(LiernePluginCatalog) && !descriptor.IsKeyedService)
            ?.ImplementationInstance is LiernePluginCatalog catalog)
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
        LiernePluginCatalog catalog,
        Func<LiernePluginCatalog, IEnumerable<LierneDiagnostic>> validate)
    {
        var diagnostics = validate(catalog).ToArray();
        if (diagnostics.Any(diagnostic => diagnostic.Severity == LierneDiagnosticSeverity.Error))
        {
            throw new LierneCompositionException(diagnostics);
        }
    }

    private sealed record CatalogValidation(Func<LiernePluginCatalog, IEnumerable<LierneDiagnostic>> Validate);
}
