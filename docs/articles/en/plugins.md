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

## Lay out document views

By default every view of a document type appears in one tab strip. The document type can declare
`DocumentDescriptor.ViewLayout` instead: a tree of tab groups and splits. The tree is code-only and
does not depend on Flare.

```csharp
builder.AddDocument(new DocumentDescriptor(
    Query, StringKey.Literal("Query"), null, typeof(QueryDocument))
{
    ViewLayout = new DocumentViewSplitLayout(
        new DocumentViewTabsLayout([SqlView]),
        new DocumentViewTabsLayout { Placement = DocumentViewTabPlacement.Bottom },
        DocumentViewSplitAxis.Vertical,
        0.55)
});
```

- `DocumentViewTabsLayout` lists its views explicitly. A null or empty list collects every view that
  no other part of the tree references, in catalog order. `Placement` is `Top` (the default) or
  `Bottom`, so a results group can keep its tab strip at the bottom of the document.
- `DocumentViewSplitLayout` places two layouts left and right (`Horizontal`) or above and below
  (`Vertical`). `FirstFraction` sets the initial share of the first branch; the host clamps it to
  0.05..0.95.
- A view the tree does not reference is appended as a tab to the last tab group in tree order, so a
  later plugin view (a profiler or a visual query builder) stays visible without changing the
  descriptor.

The host gives every group and split pane the full document height (`FlareTabs` with `FillHeight`
and flex wrappers), so a view can size itself to 100% height. It remembers the selected view of each
group and the share of each split per document instance.
`ILierneDocumentService.SetGroupSelectedView(instanceId, groupIndex, view)` and
`SetSplitFraction(instanceId, splitIndex, fraction)` change them; selecting a view in a group also
updates the document's `SelectedView`, so commands and persistence see the active view. Group and
split indices follow the resolved tree order and are stored in the workspace snapshot. A descriptor
`DefaultView` is selected in the group that contains it.

The catalog rejects a layout that references an unknown view, repeats a view, or declares a split
without a branch, with an unknown axis, or with a first-branch share outside 0.05..0.95. The
diagnostic codes are `lattice.documentviewlayout.unknown_view`,
`lattice.documentviewlayout.duplicate_view` and `lattice.documentviewlayout.invalid_split`.

Use `StringKey.Literal` for static text or provide a resource catalog with
`AddStrings(new LierneStringCatalog(namespace, resourceManager))`. The neutral resource file
supplies fallback strings. A host-provided `ILierneIconResolver` can turn descriptor icon keys into
Flare icons.

Keep IDs stable across versions. The catalog reports duplicate IDs, missing dependencies,
cycles and invalid command or group references through `LierneCompositionException`.
Commands can declare an argument type and a `CanExecute` predicate. The ordinary menu and ribbon
entries invoke commands without an argument. Register a custom component for a command that needs
an argument.
