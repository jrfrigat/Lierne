using Microsoft.AspNetCore.Components;

namespace Lattice;

/// <summary>The built-in settings document: pages on the left, generated settings on the right.</summary>
public partial class LatticeOptionsDocument
{
    private SettingsPageId? _selectedPageId;
    private string? _error;

    private IReadOnlyList<SettingsPageDescriptor> Pages =>
        Catalog.SettingsPages
            .OrderBy(page => page.Order)
            .ThenBy(page => page.Title.ToString(), StringComparer.Ordinal)
            .ToArray();

    private SettingsPageDescriptor? SelectedPage =>
        Pages.FirstOrDefault(page => page.Id == _selectedPageId) ?? Pages.FirstOrDefault();

    private IReadOnlyList<SettingDescriptor> CurrentSettings =>
        SelectedPage is { } page
            ? Catalog.Settings
                .Where(setting => setting.Page == page.Id)
                .OrderBy(setting => setting.Key.Value, StringComparer.Ordinal)
                .ToArray()
            : [];

    private string PageListLabel => Localizer.Get(new StringKey("lattice", "options"));

    private string NoSettingsText => Localizer.Get(new StringKey("lattice", "noSettings"));

    private void Select(SettingsPageId pageId) => _selectedPageId = pageId;

    private int? ReadInt(SettingsKey key) =>
        Settings.GetValue(key) is int value ? value : null;

    private bool ReadBool(SettingsKey key) => Settings.GetValue(key) is true;

    private void Set(SettingDescriptor setting, object? value)
    {
        var result = Settings.Set(setting.Key, value);
        _error = result.Success ? null : result.ErrorMessage;
    }
}
