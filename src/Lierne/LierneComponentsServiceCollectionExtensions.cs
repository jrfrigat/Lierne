using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lierne;

/// <summary>Registers the Flare host services of Lierne.</summary>
public static class LierneComponentsServiceCollectionExtensions
{
    /// <summary>Adds the host services; the application still registers Flare and Lierne.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddLierneComponents(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddLierneValidation(ValidateComponents);
        services.TryAddSingleton<ILierneIconResolver, NullLierneIconResolver>();
        services.TryAddScoped<LierneNotificationService>();
        services.TryAddScoped<LiernePaletteService>();
        return services;
    }

    private static IEnumerable<LierneDiagnostic> ValidateComponents(LiernePluginCatalog catalog)
    {
        var contributions = catalog.Documents
            .Select(item => (item.Id.Value, item.Component, typeof(LierneDocumentContext)))
            .Concat(catalog.DocumentViews
                .Select(item => (item.Id.Value, item.Component, typeof(LierneDocumentViewContext))))
            .Concat(catalog.ToolWindows.Select(item => (item.Id.Value, item.Component, typeof(LierneToolWindowContext))))
            .Concat(catalog.Toolbars.Select(item => (item.Id.Value, item.Component, typeof(LierneToolbarContext))))
            .Concat(catalog.StatusBarItems.Where(item => item.Component is not null)
                .Select(item => (item.Id.Value, item.Component!, typeof(LierneStatusBarContext))));
        foreach (var (id, component, contextType) in contributions)
        {
            if (!IsComponent(component))
            {
                yield return InvalidComponent(id, component);
                continue;
            }

            if (!component.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(property =>
                    property.Name == nameof(LierneDocumentComponent.Context) &&
                    property.SetMethod?.IsPublic == true &&
                    property.PropertyType.IsAssignableFrom(contextType) &&
                    property.GetCustomAttribute<ParameterAttribute>(inherit: true) is not null))
            {
                yield return LierneDiagnostic.Error(
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

    private static LierneDiagnostic InvalidComponent(string id, Type? component) =>
        LierneDiagnostic.Error("lattice.component.invalid_type",
            $"Contribution '{id}' must use a concrete Blazor component. Registered type: '{component}'.");
}
