# Compose plugins and contributions

[Русский](../ru/plugins.md)

The host chooses its plugins during startup. Each plugin implements `ILatticePlugin`, declares a
stable ID, lists its dependencies and registers contributions through `ILatticePluginBuilder`.
`AddLattice` calls each plugin's `Configure` in dependency order and creates a shared catalog.
Runtime services, including commands, documents, settings and observable context, belong to the
current application scope.

## Add a document and a command

This plugin opens a new document through a command. Add it to the host with
`options.AddPlugin<WelcomePlugin>()`. The `WelcomeDocument` component below receives its document
and scope services through `Context`.

```csharp
using Lattice;
using Microsoft.Extensions.DependencyInjection;

public sealed class WelcomePlugin : ILatticePlugin
{
    private static readonly DocumentTypeId Welcome = new("sample.welcome");
    private static readonly CommandId Open = new("sample.welcome.open");
    private static readonly RibbonGroupId Group = new("sample.welcome.ribbon");

    public PluginId Id => new("sample.welcome");
    public StringKey DisplayName => StringKey.Literal("Welcome");
    public IReadOnlyList<PluginId> DependsOn => [];

    public void Configure(ILatticePluginBuilder builder)
    {
        builder.AddDocument(new DocumentDescriptor(
            Welcome, StringKey.Literal("Welcome"), null, typeof(WelcomeDocument)));

        builder.AddCommand(new CommandDescriptor(Open, StringKey.Literal("Open welcome")),
            static (context, _) =>
            {
                var documents = context.Services.GetRequiredService<ILatticeDocumentService>();
                var result = documents.Open(Welcome);
                return Task.FromResult(result.Success
                    ? LatticeCommandResult.Ok(result.Data)
                    : LatticeCommandResult.Fail(result.ErrorCode!, result.ErrorMessage!));
            });

        builder.AddRibbonGroup(new RibbonGroupDescriptor(Group, DisplayName));
        builder.AddRibbonItem(new RibbonItemDescriptor(
            new RibbonItemId("sample.welcome.open.ribbon"), Group, Open));
    }
}
```

```razor
@inherits Lattice.LatticeDocumentComponent

<p>@Context.Localizer.Get(Context.Document.Title)</p>
```

## Choose a contribution

| To add | Register |
| --- | --- |
| Openable document or docked tool window | `AddDocument` or `AddToolWindow` |
| Command and keyboard shortcut | `AddCommand` with a `CommandDescriptor` |
| Ribbon or menu entry that invokes a command | `AddRibbonItem` or `AddMenuItem` |
| Custom toolbar, status bar item or shell content | `AddToolbar`, `AddStatusBarItem` or `AddShellContent` |
| Options page and its typed settings | `AddSettingsPage` and `AddSetting` |
| Strings or a settings import/export section | `AddStrings` or `AddSettingsTransfer` |

A tool window derives from `LatticeToolWindowComponent` and can live on the left, right or bottom.
A document derives from `LatticeDocumentComponent` and can move between the center and side
regions. Both expose the scoped services through their respective `Context`.

Use `StringKey.Literal` for static text or provide a resource catalog with
`AddStrings(new LatticeStringCatalog(namespace, resourceManager))`. The neutral resource file
supplies fallback strings. A host-provided `ILatticeIconResolver` can turn descriptor icon keys into
Flare icons.

Keep IDs stable across versions. The catalog reports duplicate IDs, missing dependencies,
cycles and invalid command or group references through `LatticeCompositionException`.
Commands can declare an argument type and a `CanExecute` predicate. The ordinary menu and ribbon
entries invoke commands without an argument. Register a custom component for a command that needs
an argument.
