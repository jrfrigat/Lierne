# Changelog

[Русский](CHANGELOG.ru.md)

## 0.2.0

### Added

- Search all plugin commands in Settings, including commands without an assigned shortcut. Hosts can list commands they register separately via `ILierneCommandSettingsSource`.
- Open additional document views from the tab bar when `DocumentViewTabsLayout.ShowAdditionalViewsMenu` is enabled.
- Open dock tab actions from a context menu when the host enables `LierneShell.UseTabContextMenu`.

### Changed

- Plugin settings use `FlareSettings`, with searchable categories and a resizable navigation pane.
- Side dock groups shorten tab labels to fit and switch to icons in narrow spaces. The leading icon serves as the drag handle.
- Lierne now requires `Flare.Components.IDE` 0.62.0 or later. Update any Flare packages pinned to older versions in the host.

### Fixed

- Removed gaps between the rows of the IDE shell.

## 0.1.1

- Updated Flare to 0.61.1.
- Updated dock tab selection and markers without rebuilding the workspace layout.

## 0.1.0

- First NuGet release of `Lierne`, `Lierne.Abstractions` and `Lierne.Core` for .NET 11.
- Added plugin composition for documents, tool windows, commands and settings, with a Flare-based IDE shell and workspace persistence.
