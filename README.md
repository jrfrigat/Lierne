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
- `Lattice.Core.Tests` - tests for ordering, diagnostics, execution, state and registration.

The Flare UI host (`Lattice.Components`), persistent workspace state and the first real plugin
(Quas database connection) are the next increments; plugins live in the consuming application, not
in Lattice. See [docs/architecture.md](docs/architecture.md).

## Quick start

```csharp
services.AddLattice(options => options
    .AddPlugin<CorePlugin>()
    .AddPlugin<ConnectionPlugin>());

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

## Build and test

```sh
dotnet build Lattice.slnx -c Release
dotnet test Lattice.slnx -c Release --no-build
```

## License

MIT. See the `PackageLicenseExpression` in `Directory.Build.props`.
