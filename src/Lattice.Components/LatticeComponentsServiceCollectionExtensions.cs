using System.Reflection;
using Microsoft.AspNetCore.Components;
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
        services.AddLatticeValidation(ValidateComponents);
        services.TryAddSingleton<ILatticeIconResolver, NullLatticeIconResolver>();
        return services;
    }

    private static IEnumerable<LatticeDiagnostic> ValidateComponents(LatticePluginCatalog catalog)
    {
        var contributions = catalog.Documents
            .Select(item => (item.Id.Value, item.Component, typeof(LatticeDocumentContext)))
            .Concat(catalog.ToolWindows.Select(item => (item.Id.Value, item.Component, typeof(LatticeToolWindowContext))))
            .Concat(catalog.Toolbars.Select(item => (item.Id.Value, item.Component, typeof(LatticeToolbarContext))))
            .Concat(catalog.StatusBarItems.Where(item => item.Component is not null)
                .Select(item => (item.Id.Value, item.Component!, typeof(LatticeStatusBarContext))));
        foreach (var (id, component, contextType) in contributions)
        {
            if (!IsComponent(component))
            {
                yield return InvalidComponent(id, component);
                continue;
            }

            if (!component.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(property =>
                    property.Name == nameof(LatticeDocumentComponent.Context) &&
                    property.SetMethod?.IsPublic == true &&
                    property.PropertyType.IsAssignableFrom(contextType) &&
                    property.GetCustomAttribute<ParameterAttribute>(inherit: true) is not null))
            {
                yield return LatticeDiagnostic.Error(
                    "lattice.component.invalid_context",
                    $"Contribution '{id}' uses '{component}' without a public Context parameter accepting {contextType.Name}.");
            }
        }

        var surfaces = catalog.ShellContents.Select(item => (item.Id.Value, item.Component))
            .Concat(catalog.SettingsPages.Where(item => item.Component is not null)
                .Select(item => (item.Id.Value, item.Component!)));
        foreach (var (id, component) in surfaces)
        {
            if (!IsComponent(component))
            {
                yield return InvalidComponent(id, component);
            }
        }
    }

    private static bool IsComponent(Type? component) =>
        component is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false } &&
        typeof(IComponent).IsAssignableFrom(component);

    private static LatticeDiagnostic InvalidComponent(string id, Type? component) =>
        LatticeDiagnostic.Error("lattice.component.invalid_type",
            $"Contribution '{id}' must use a concrete Blazor component. Registered type: '{component}'.");
}
