# Compose plugins and contributions

[Русский](../ru/plugins.md)

The host chooses its plugins during startup. Each plugin implements `ILiernePlugin`, declares a
stable ID, lists its dependencies and registers contributions through `ILiernePluginBuilder`.
`AddLierne` calls each plugin's `Configure` in dependency order and creates a shared catalog.
Runtime services, including commands, documents, settings and observable context, belong to the
current application scope.

## Add a document and a command

This plugin opens a new document through a command. Add it to the host with
`options.AddPlugin<WelcomePlugin>()`. The `WelcomeDocument` component below receives its document
and scope services through `Context`.

```csharp
using Lierne;
using Microsoft.Extensions.DependencyInjection;

public sealed class WelcomePlugin : ILiernePlugin
{
    private static readonly DocumentTypeId Welcome = new("sample.welcome");
    private static readonly CommandId Open = new("sample.welcome.open");
    private static readonly RibbonGroupId Group = new("sample.welcome.ribbon");

    public PluginId Id => new("sample.welcome");
    public StringKey DisplayName => StringKey.Literal("Welcome");
    public IReadOnlyList<PluginId> DependsOn => [];

    public void Configure(ILiernePluginBuilder builder)
    {
        builder.AddDocument(new DocumentDescriptor(
            Welcome, StringKey.Literal("Welcome"), null, typeof(WelcomeDocument)));

        builder.AddCommand(new CommandDescriptor(Open, StringKey.Literal("Open welcome")),
            static (context, _) =>
            {
                var documents = context.Services.GetRequiredService<ILierneDocumentService>();
                var result = documents.Open(Welcome);
                return Task.FromResult(result.Success
                    ? LierneCommandResult.Ok(result.Data)
                    : LierneCommandResult.Fail(result.ErrorCode!, result.ErrorMessage!));
            });

        builder.AddRibbonGroup(new RibbonGroupDescriptor(Group, DisplayName));
        builder.AddRibbonItem(new RibbonItemDescriptor(
            new RibbonItemId("sample.welcome.open.ribbon"), Group, Open));
    }
}
```

```razor
@inherits Lierne.LierneDocumentComponent

<p>@Context.Localizer.Get(Context.Document.Title)</p>
```

## Choose a contribution

| To add | Register |
| --- | --- |
| Openable document or docked tool window | `AddDocument` or `AddToolWindow` |
| Extra tab on another plugin's document type | `AddDocumentView` with a `DocumentViewDescriptor` |
| Command and keyboard shortcut | `AddCommand` with a `CommandDescriptor` |
| Ribbon or menu entry that invokes a command | `AddRibbonItem` or `AddMenuItem` |
| Custom toolbar, status bar item or shell content | `AddToolbar`, `AddStatusBarItem` or `AddShellContent` |
| Options page and its typed settings | `AddSettingsPage` and `AddSetting` |
| Strings or a settings import/export section | `AddStrings` or `AddSettingsTransfer` |

A tool window derives from `LierneToolWindowComponent` and can live on the left, right or bottom.
A document derives from `LierneDocumentComponent` and can move between the center and side
regions. Both expose the scoped services through their respective `Context`.

A document view adds a tab to a document type declared by any plugin and refers to it only by its
`DocumentTypeId`, so plugins do not need to reference each other. The host reads the views from
`LiernePluginCatalog.ViewsFor` and renders them itself.

Use `StringKey.Literal` for static text or provide a resource catalog with
`AddStrings(new LierneStringCatalog(namespace, resourceManager))`. The neutral resource file
supplies fallback strings. A host-provided `ILierneIconResolver` can turn descriptor icon keys into
Flare icons.

Keep IDs stable across versions. The catalog reports duplicate IDs, missing dependencies,
cycles and invalid command or group references through `LierneCompositionException`.
Commands can declare an argument type and a `CanExecute` predicate. The ordinary menu and ribbon
entries invoke commands without an argument. Register a custom component for a command that needs
an argument.
