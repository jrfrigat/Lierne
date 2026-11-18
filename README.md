# Lattice

Lattice is a composition framework for IDE-style applications built on
[Flare](https://github.com/jrfrigat/Flare). An application is assembled from plugins - compile-time
components, not dynamically loaded packages - and each plugin declares the documents, tool windows,
commands, ribbon items and settings it brings.

The framework is being extracted from [Quas](https://github.com/jrfrigat/Quas), the database IDE that
is its first consumer. Quas remains the proving ground: the first real plugin is its database
connection (Database Explorer plus the connection toolbox).

## Status

The architecture is being built incrementally. What exists today:

- `Lattice.Abstractions` - typed ids, plugin contract, contribution descriptors, runtime contracts;
- `Lattice.Core` - plugin catalog with dependency ordering and validation, command execution with
  observers and shortcuts, documents and tool windows, schema-driven settings, observable context,
  localized string catalogs;
- `Lattice.Components` - the Flare IDE host: `LatticeShell`, ribbon, menus, documents, tool windows
  and the icon resolver; plugin components derive from `LatticeDocumentComponent` or
  `LatticeToolWindowComponent`. `LatticeHostPlugin` brings the built-in Options document (generated
  from the settings schema) and the Tools menu group;
- `Lattice.Core.Tests` and `Lattice.Components.Tests` - composition, runtime and shell rendering.

Quas already uses Lattice for its application plugins. Lattice can save preferences and restore
the workspace when the application registers storage. The [architecture guide](docs/architecture.md)
describes the persistence contract.

## Quick start

```csharp
services.AddFlare();                 // the application chooses its theme
services.AddLattice(options => options
    .AddPlugin<LatticeHostPlugin>()     // Options document + Tools menu group
    .AddPlugin<CorePlugin>()
    .AddPlugin<ConnectionPlugin>());
services.AddLatticeComponents();     // icon resolver and host services
services.AddLatticeBrowserStateStorage("my-ide"); // optional in an interactive web host

public sealed class ConnectionPlugin : ILatticePlugin
{
    public PluginId Id => new("app.connection");

    public StringKey DisplayName => new("app.connection", "displayName");

    public IReadOnlyList<PluginId> DependsOn => [new PluginId("app.core")];

    public void Configure(ILatticePluginBuilder builder)
    {
        builder.AddDocument(new DocumentDescriptor(
            new DocumentTypeId("app.connection.explorer"),
            new StringKey("app.connection", "explorer"),
            "database",
            typeof(DatabaseExplorerDocument)));
    }
}
```

Composition is validated before the host starts: duplicate ids, missing dependencies, dependency
cycles and ribbon items that invoke unknown commands fail with a diagnostic that names both plugins.

For a desktop or Hybrid host, register `ILatticeStateStorage` with a
`LatticeFileStateStorage` pointing to the application's data directory. In a web host,
`AddLatticeBrowserStateStorage` uses the current browser and origin. State is restored when
`LatticeShell` first becomes interactive.

## Sample IDE

```sh
dotnet run --project samples/Lattice.SampleIde
```

Open a note, edit it, and reload the page to restore it. Open **Tools > Options** to assign a
shortcut or set the default title for new notes. The sample includes a versioned document payload
codec and uses browser storage.

## Build and test

```sh
dotnet build Lattice.slnx -c Release
dotnet test Lattice.slnx -c Release --no-build
```

The build uses Flare 0.59.0 from NuGet by default. To build against the sibling Flare checkout, copy
`Directory.Build.local.props.example` to the git-ignored `Directory.Build.local.props`;
`UseLocalFlare=true` swaps the package references for project references.

To create local NuGet packages, run
`dotnet pack Lattice.slnx -c Release -p:UseLocalFlare=false -o artifacts/packages`.
Add the output directory as a package source and reference `Lattice.Components` in an IDE host.

## License

MIT. See the `PackageLicenseExpression` in `Directory.Build.props`.
