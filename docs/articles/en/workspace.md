# Customize and persist a workspace

[Русский](../ru/workspace.md)

`LierneShell` builds the IDE layout from the plugin catalog. Use the start page or the standard
shell slots when the application needs its own content. A plugin can contribute a toolbar,
a status bar item, or a component for a supported slot.

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

Lierne passes the same template to `FlareDocumentTab.HeaderContent` and
`FlareIdeLayout.DocumentHeaderContent`, so the center and side regions share one header.

## Choose storage

Register storage to restore preferences, keyboard shortcuts, panel layout and open documents.
`LierneShell` restores saved state when it becomes interactive.

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
document positions and selection. `LierneFileStateStorage` replaces each file atomically.
A host can implement `ILierneStateStorage` for a different store.

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
