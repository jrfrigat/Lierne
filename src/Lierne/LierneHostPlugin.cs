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

    /// <summary>The command that opens the searchable command palette.</summary>
    public static readonly CommandId OpenPaletteCommand = new("lattice.palette.open");

    /// <summary>The command that pins the active document tab.</summary>
    public static readonly CommandId PinDocumentCommand = new("lattice.document.pin");

    /// <summary>The command that unpins the active document tab.</summary>
    public static readonly CommandId UnpinDocumentCommand = new("lattice.document.unpin");

    /// <summary>The command that turns the active document tab into a preview.</summary>
    public static readonly CommandId PreviewDocumentCommand = new("lattice.document.preview");

    /// <summary>The built-in Commands settings page.</summary>
    public static readonly SettingsPageId CommandsPage = new("lattice.settings.commands");

    /// <summary>The built-in Themes settings page.</summary>
    public static readonly SettingsPageId ThemesPage = new("lattice.settings.themes");

    /// <summary>The built-in Import/Export settings page.</summary>
    public static readonly SettingsPageId TransferPage = new("lattice.settings.transfer");

    /// <summary>The built-in Tools menu group; applications add their items to this group.</summary>
    public static readonly MenuGroupId ToolsMenuGroup = new("lattice.tools");

    /// <summary>The built-in Document menu group for commands that act on the active document.</summary>
    public static readonly MenuGroupId DocumentMenuGroup = new("lattice.document");

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
        builder.AddCommand(
            new CommandDescriptor(
                OpenPaletteCommand,
                new StringKey("lattice", "commandPalette"),
                "search",
                "Ctrl+Shift+P",
                Description: new StringKey("lattice", "commandPaletteDescription")),
            OpenPaletteAsync);
        builder.AddCommand(
            new CommandDescriptor(
                PinDocumentCommand,
                new StringKey("lattice", "pinDocument"),
                "pin"),
            PinDocumentAsync);
        builder.AddCommand(
            new CommandDescriptor(
                UnpinDocumentCommand,
                new StringKey("lattice", "unpinDocument"),
                "unpin"),
            UnpinDocumentAsync);
        builder.AddCommand(
            new CommandDescriptor(
                PreviewDocumentCommand,
                new StringKey("lattice", "previewDocument"),
                "preview"),
            PreviewDocumentAsync);
        builder.AddCommandUpdateHandler(
            PinDocumentCommand,
            new ActiveDocumentCommandHandler(
                PinDocumentCommand,
                new StringKey("lattice", "pinDocument"),
                "pin",
                document => !document.Pinned));
        builder.AddCommandUpdateHandler(
            UnpinDocumentCommand,
            new ActiveDocumentCommandHandler(
                UnpinDocumentCommand,
                new StringKey("lattice", "unpinDocument"),
                "unpin",
                document => document.Pinned));
        builder.AddCommandUpdateHandler(
            PreviewDocumentCommand,
            new ActiveDocumentCommandHandler(
                PreviewDocumentCommand,
                new StringKey("lattice", "previewDocument"),
                "preview",
                document => !document.Preview));
        builder.AddMenuGroup(new MenuGroupDescriptor(
            ToolsMenuGroup,
            new StringKey("lattice", "tools"),
            900));
        builder.AddCommandPlacement(new CommandPlacementDescriptor(
            OpenPaletteCommand,
            CommandSurface.Menu,
            ToolsMenuGroup.Value,
            0));
        builder.AddMenuItem(new MenuItemDescriptor(
            new MenuItemId("lattice.tools.options"),
            ToolsMenuGroup,
            new StringKey("lattice", "openOptions"),
            OpenOptionsCommand,
            100));
        builder.AddMenuGroup(new MenuGroupDescriptor(
            DocumentMenuGroup,
            new StringKey("lattice", "document"),
            800));
        builder.AddCommandPlacement(new CommandPlacementDescriptor(
            PinDocumentCommand,
            CommandSurface.Menu,
            DocumentMenuGroup.Value,
            10));
        builder.AddCommandPlacement(new CommandPlacementDescriptor(
            UnpinDocumentCommand,
            CommandSurface.Menu,
            DocumentMenuGroup.Value,
            20));
        builder.AddCommandPlacement(new CommandPlacementDescriptor(
            PreviewDocumentCommand,
            CommandSurface.Menu,
            DocumentMenuGroup.Value,
            30));
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

    private static Task<LierneCommandResult> OpenPaletteAsync(
        LierneCommandContext context,
        CancellationToken cancellationToken)
    {
        context.Services.GetRequiredService<LiernePaletteService>().Open();
        return Task.FromResult(LierneCommandResult.Ok());
    }

    private static Task<LierneCommandResult> PinDocumentAsync(
        LierneCommandContext context,
        CancellationToken cancellationToken) =>
        UpdateActiveDocumentAsync(context, document =>
        {
            var preview = document.Preview ? context.Services.GetRequiredService<ILierneDocumentService>()
                .SetPreview(document.Id, false) : LierneResult.Ok;
            return preview.Success
                ? context.Services.GetRequiredService<ILierneDocumentService>().SetPinned(document.Id, true)
                : preview;
        });

    private static Task<LierneCommandResult> UnpinDocumentAsync(
        LierneCommandContext context,
        CancellationToken cancellationToken) =>
        UpdateActiveDocumentAsync(context, document =>
            context.Services.GetRequiredService<ILierneDocumentService>().SetPinned(document.Id, false));

    private static Task<LierneCommandResult> PreviewDocumentAsync(
        LierneCommandContext context,
        CancellationToken cancellationToken) =>
        UpdateActiveDocumentAsync(context, document =>
        {
            var documents = context.Services.GetRequiredService<ILierneDocumentService>();
            var unpinned = documents.SetPinned(document.Id, false);
            return unpinned.Success ? documents.SetPreview(document.Id, true) : unpinned;
        });

    private static Task<LierneCommandResult> UpdateActiveDocumentAsync(
        LierneCommandContext context,
        Func<LierneDocumentInstance, LierneResult> update)
    {
        var documents = context.Services.GetRequiredService<ILierneDocumentService>();
        if (documents.Active is not { } document)
        {
            return Task.FromResult(LierneCommandResult.Fail(
                "lattice.document.none",
                "No document is active."));
        }

        var result = update(document);
        return Task.FromResult(result.Success
            ? LierneCommandResult.Ok()
            : LierneCommandResult.Fail(
                result.ErrorCode ?? "lattice.document.failed",
                result.ErrorMessage ?? "The document command failed."));
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

    private sealed class ActiveDocumentCommandHandler(
        CommandId command,
        StringKey title,
        string iconKey,
        Func<LierneDocumentInstance, bool> enabled) : ICommandUpdateHandler
    {
        public ValueTask<CommandPresentation> UpdateAsync(
            LierneCommandContext context,
            CancellationToken cancellationToken = default)
        {
            var document = context.Services.GetService<ILierneDocumentService>()?.Active;
            return ValueTask.FromResult(new CommandPresentation(
                command,
                title,
                IconKey: iconKey,
                Enabled: document is not null && enabled(document),
                Visible: true));
        }
    }
}
