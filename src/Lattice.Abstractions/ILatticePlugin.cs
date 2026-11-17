using Microsoft.Extensions.DependencyInjection;

namespace Lattice;

/// <summary>
/// A compile-time component of a Lattice application: the host references it, lists it and the
/// plugin declares the documents, tool windows, commands, ribbon items and settings it brings.
/// </summary>
public interface ILatticePlugin
{
    /// <summary>Gets the stable plugin id.</summary>
    PluginId Id { get; }

    /// <summary>Gets the localized display name of the plugin.</summary>
    StringKey DisplayName { get; }

    /// <summary>
    /// Gets the plugins that must be composed before this one. The host orders plugins by these
    /// edges and reports a missing dependency or a cycle instead of composing in an arbitrary order.
    /// </summary>
    IReadOnlyList<PluginId> DependsOn { get; }

    /// <summary>Declares the services and contributions of the plugin.</summary>
    /// <param name="builder">The builder that collects the plugin contributions.</param>
    /// <remarks>
    /// The host calls this once, at composition time, in dependency order. The method registers
    /// service descriptors and contribution descriptors; it does not resolve services or build UI.
    /// </remarks>
    void Configure(ILatticePluginBuilder builder);
}

/// <summary>Collects the services and contributions a plugin declares.</summary>
public interface ILatticePluginBuilder
{
    /// <summary>Gets the service collection the plugin registers its services into.</summary>
    IServiceCollection Services { get; }

    /// <summary>Gets the documents declared so far.</summary>
    IReadOnlyList<DocumentDescriptor> Documents { get; }

    /// <summary>Gets the tool windows declared so far.</summary>
    IReadOnlyList<ToolWindowDescriptor> ToolWindows { get; }

    /// <summary>Gets the commands declared so far.</summary>
    IReadOnlyList<CommandDescriptor> Commands { get; }

    /// <summary>Gets the ribbon groups declared so far.</summary>
    IReadOnlyList<RibbonGroupDescriptor> RibbonGroups { get; }

    /// <summary>Gets the ribbon items declared so far.</summary>
    IReadOnlyList<RibbonItemDescriptor> RibbonItems { get; }

    /// <summary>Gets the settings pages declared so far.</summary>
    IReadOnlyList<SettingsPageDescriptor> SettingsPages { get; }

    /// <summary>Gets the settings declared so far.</summary>
    IReadOnlyList<SettingDescriptor> Settings { get; }

    /// <summary>Declares a document type.</summary>
    /// <param name="descriptor">The document descriptor.</param>
    void AddDocument(DocumentDescriptor descriptor);

    /// <summary>Declares a tool window.</summary>
    /// <param name="descriptor">The tool window descriptor.</param>
    void AddToolWindow(ToolWindowDescriptor descriptor);

    /// <summary>Declares a command.</summary>
    /// <param name="descriptor">The command descriptor.</param>
    void AddCommand(CommandDescriptor descriptor);

    /// <summary>Declares a ribbon group.</summary>
    /// <param name="descriptor">The group descriptor.</param>
    void AddRibbonGroup(RibbonGroupDescriptor descriptor);

    /// <summary>Declares a ribbon item.</summary>
    /// <param name="descriptor">The item descriptor.</param>
    void AddRibbonItem(RibbonItemDescriptor descriptor);

    /// <summary>Declares a settings page.</summary>
    /// <param name="descriptor">The page descriptor.</param>
    void AddSettingsPage(SettingsPageDescriptor descriptor);

    /// <summary>Declares a setting.</summary>
    /// <param name="descriptor">The setting descriptor.</param>
    void AddSetting(SettingDescriptor descriptor);
}
