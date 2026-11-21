<p align="center">
  <img src="assets/banner.svg" alt="Lierne" width="860">
</p>

# Lierne

<p align="center"><strong>English</strong> · <a href="README.ru.md">Русский</a></p>

[![.NET 11](https://img.shields.io/badge/.NET-11-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/license-MIT-267A62)](LICENSE)

Lierne is a .NET 11 workspace framework for building domain IDEs on Blazor and
[Flare](https://github.com/jrfrigat/Flare). It does not ship a code editor: documents are domain
objects with pluggable views. Plugins contribute documents, tool windows, commands and settings.
Lierne validates those contributions and assembles them into a shared workspace. Quas, a database
IDE, is its first consumer.

- **Compose at startup.** Register plugins in the host and declare their dependencies. The catalog
  reports conflicting identifiers, missing dependencies, cycles and invalid references before the UI starts.
- **Share commands across plugins.** Use the same commands from menus, the ribbon and keyboard
  shortcuts. Add localized strings and settings alongside the features that use them.
- **Restore the workspace.** Register browser or file storage to save user preferences, panel
  layout and eligible documents.

## Get started

The main NuGet package ID is `Lierne`. When working from a source checkout, reference
`src/Lierne/Lierne.csproj`.

Try the interactive sample with the .NET 11 SDK.

```sh
dotnet run --project samples/Lierne.SampleIde
```

Choose **New note**, enter text and reload the page to see the note restored. The sample also
demonstrates a tool window, a keyboard shortcut and an options page contributed by one plugin.

## Documentation

- [Set up a Blazor host](docs/articles/en/getting-started.md)
- [Compose plugins and contributions](docs/articles/en/plugins.md)
- [Customize and persist a workspace](docs/articles/en/workspace.md)
- [Architecture notes (Russian)](docs/architecture.md)

## License

[MIT](LICENSE)
