# Customize and persist a workspace

[Русский](../ru/workspace.md)

`LierneShell` composes the IDE from the plugin catalog: a command bar, a dockable workspace,
a status bar, a command palette and a notification host. Use the start page or the standard shell
slots when the application needs its own content. A plugin can contribute a toolbar, a status bar
item, or a component for a supported slot.

The workspace maps documents and tool windows to `FlareDockWorkspace`. Users can move tabs, split
groups, reorder and resize them; Lierne accepts the change and updates the document and panel
services. The referenced Flare package must provide `FlareDockWorkspace`.

## Compose the shell

`LierneShellOptions` turns individual surfaces on or off. The `CommandBarContent`,
`WorkspaceContent`, `CommandPaletteContent` and `NotificationsContent` parameters replace a surface
with application markup. A host can therefore keep the Lierne workspace but supply its own chrome,
or use `LierneCommandBar`, `LierneWorkspace`, `LierneStatusBar`, `LierneCommandPalette` and
`LierneNotificationHost` separately.

```razor
<LierneShell Options="@(new LierneShellOptions { ShowRibbon = false })">
    <StatusBar>Custom status</StatusBar>
</LierneShell>
```

## Limit dock drop zones

Set `DropZones` on `LierneShell` or `LierneWorkspace` to the `DockDropZones` flags the host offers.
For example, keeping only the center and horizontal edges removes the vertical split targets.

```razor
<LierneShell DropZones="@(DockDropZones.Into | DockDropZones.Start | DockDropZones.End)" />
```

## Hide the drop guide

Set `ShowDropGuide="false"` on `LierneShell` or `LierneWorkspace` to keep the drop zones active
without Flare's base highlight. The host can present its own guide.

```razor
<LierneShell ShowDropGuide="false" />
```

## Customize document tabs

Supply a single `DocumentHeaderContent` template for the center, left, and right regions.
The template receives the current document and localized title. It can show a status or category
from the application alongside the title. The tab keeps its accessible name and controls.

```razor
@using Flare.Components
@using Flare.Icons
@using Lierne

<LierneShell>
    <DocumentHeaderContent Context="tab">
        <FlareIconView Value="@FlareIcons.PushPin" />
        @tab.Title
    </DocumentHeaderContent>
</LierneShell>
```

Lierne uses the same template in the workspace tab header for the center, left and right docks.
The tab keeps its accessible name and close control.

## Choose storage

Register storage to restore preferences, keyboard shortcuts, panel layout and open documents.
`LierneWorkspace` restores saved state when it becomes interactive and applies saved panel
placements.

For a browser host, use a stable key for the app. Data stays in local storage for the current
browser and origin.

```csharp
services.AddLierneBrowserStateStorage("my-ide");
```

For a desktop or Blazor Hybrid host, register file storage in the application's data directory.

```csharp
using Lierne;
using Microsoft.Extensions.DependencyInjection;

var stateDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "MyIde");

services.AddScoped<ILierneStateStorage>(_ => new LierneFileStateStorage(stateDirectory));
```

Preferences and workspace state are separate versioned JSON sections. Preferences contain
setting overrides and keyboard shortcuts. The workspace contains tool visibility, panel layout,
document positions and selection, the selected view of each document view group and the share of
each document split. `LierneFileStateStorage` replaces each file atomically. A host can implement
`ILierneStateStorage` for a different store.

## Restore document content

A document type is eligible for restore by default. Set `Persistent: false` on a document
descriptor to exclude it. A document with a non-null payload needs a `StateCodec` on its descriptor.
Use a stable type ID and keep the codec able to read the previously saved payload versions.

For a text payload, assign a codec like the one in the sample to
`DocumentDescriptor.StateCodec`.

```csharp
new LierneDocumentStateCodec(
    1,
    payload => JsonSerializer.SerializeToElement(payload as string),
    (version, payload) => version == 1
        ? payload.GetString()
        : throw new JsonException("Unsupported note payload version."))
```

When content changes, call `Context.Documents.SetPayload(Context.Document.Id, newValue)` from
the document component. The shell can combine rapid updates into one save. To wait for pending
writes during an explicit shutdown or handoff, call `LierneStatePersistence.FlushAsync()`.

The workspace reader skips a document if its type is absent or its codec cannot read the
payload. A host can register `LierneStateMigration` to upgrade an older preferences or workspace
section. An unsupported section version remains in storage.
