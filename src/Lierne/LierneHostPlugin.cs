using Microsoft.Extensions.DependencyInjection;

namespace Lierne;

/// <summary>
/// The framework plugin that brings the built-in host surfaces: the Options document, its open
/// command and the built-in Tools menu group. Applications add it explicitly in their plugin list.
/// </summary>
public sealed class LierneHostPlugin : ILiernePlugin
{
    /// <summary>The document type of the settings document.</summary>
    public static readonly DocumentTypeId OptionsDocumentType = new("lattice.options");

    /// <summary>The command that opens the settings document.</summary>
    public static readonly CommandId OpenOptionsCommand = new("lattice.options.open");

    /// <summary>The built-in Commands settings page.</summary>
    public static readonly SettingsPageId CommandsPage = new("lattice.settings.commands");

    /// <summary>The built-in Themes settings page.</summary>
    public static readonly SettingsPageId ThemesPage = new("lattice.settings.themes");

    /// <summary>The built-in Import/Export settings page.</summary>
    public static readonly SettingsPageId TransferPage = new("lattice.settings.transfer");

    /// <summary>The built-in Tools menu group; applications add their items to this group.</summary>
    public static readonly MenuGroupId ToolsMenuGroup = new("lattice.tools");

    /// <summary>Creates a key in the built-in Lierne string namespace.</summary>
    /// <param name="name">The key name.</param>
    /// <returns>The key.</returns>
    public static StringKey Key(string name) => new("lattice", name);

    /// <inheritdoc />
    public PluginId Id => new("lattice.host");

    /// <inheritdoc />
    public StringKey DisplayName => new("lattice", "host");

    /// <inheritdoc />
    public IReadOnlyList<PluginId> DependsOn => [];

    /// <inheritdoc />
    public void Configure(ILiernePluginBuilder builder)
    {
        builder.AddDocument(new DocumentDescriptor(
            OptionsDocumentType,
            new StringKey("lattice", "options"),
            "settings",
            typeof(LierneOptionsDocument),
            Persistent: false));
        builder.AddCommand(
            new CommandDescriptor(
                OpenOptionsCommand,
                new StringKey("lattice", "openOptions"),
                "settings"),
            OpenOptionsAsync);
        builder.AddMenuGroup(new MenuGroupDescriptor(
            ToolsMenuGroup,
            new StringKey("lattice", "tools"),
            900));
        builder.AddMenuItem(new MenuItemDescriptor(
            new MenuItemId("lattice.tools.options"),
            ToolsMenuGroup,
            new StringKey("lattice", "openOptions"),
            OpenOptionsCommand,
            100));
        builder.AddSettingsPage(new SettingsPageDescriptor(
            CommandsPage,
            Key("commands"),
            10,
            typeof(LierneCommandsSettingsPage),
            LierneSettingsGroup.General));
        builder.AddSettingsPage(new SettingsPageDescriptor(
            ThemesPage,
            Key("themes"),
            20,
            typeof(LierneThemesSettingsPage),
            LierneSettingsGroup.General));
        builder.AddSettingsPage(new SettingsPageDescriptor(
            TransferPage,
            Key("transfer"),
            30,
            typeof(LierneSettingsTransferSettingsPage),
            LierneSettingsGroup.General));
        builder.AddStrings(new LierneStringCatalog("lattice", LierneHostStrings.ResourceManager));
    }

    private static Task<LierneCommandResult> OpenOptionsAsync(
        LierneCommandContext context,
        CancellationToken cancellationToken)
    {
        var documents = context.Services.GetRequiredService<ILierneDocumentService>();
        var existing = documents.Documents.FirstOrDefault(document =>
            document.TypeId == OptionsDocumentType);
        if (existing is not null)
        {
            var activated = documents.Activate(existing.Id);
            return Task.FromResult(activated.Success
                ? LierneCommandResult.Ok()
                : LierneCommandResult.Fail(
                    activated.ErrorCode ?? "lattice.options.failed",
                    activated.ErrorMessage ?? "Could not activate the options document."));
        }

        var opened = documents.Open(OptionsDocumentType);
        return Task.FromResult(opened.Success
            ? LierneCommandResult.Ok()
            : LierneCommandResult.Fail(
                opened.ErrorCode ?? "lattice.options.failed",
                opened.ErrorMessage ?? "Could not open the options document."));
    }
}
