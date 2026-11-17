using Microsoft.AspNetCore.Components;

namespace Lattice;

/// <summary>The built-in settings document: a plugin tree on the left, the selected page on the right.</summary>
public partial class LatticeOptionsDocument
{
    private IReadOnlyList<SettingsGroupNode> _groups = [];
    private readonly HashSet<string> _collapsedGroups = new(StringComparer.Ordinal);
    private SettingsPageId? _selectedPageId;
    private string? _error;

    private IReadOnlyList<SettingsGroupNode> Groups => _groups;

    private IReadOnlyList<SettingsPageDescriptor> Pages =>
        _groups.SelectMany(group => group.Pages).ToArray();

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

    /// <inheritdoc />
    protected override void OnInitialized() => _groups = BuildGroups();

    private IReadOnlyList<SettingsGroupNode> BuildGroups()
    {
        var groups = new List<SettingsGroupNode>();
        var general = Catalog.SettingsPages
            .Where(page => page.Group == LatticeSettingsGroup.General)
            .OrderBy(page => page.Order)
            .ThenBy(page => page.Title.ToString(), StringComparer.Ordinal)
            .ToArray();
        if (general.Length > 0)
        {
            groups.Add(new SettingsGroupNode(
                "general",
                LatticeHostPlugin.Key("general"),
                general));
        }

        foreach (var plugin in Catalog.Plugins.OrderBy(plugin => plugin.Order))
        {
            var pages = Catalog.SettingsPages
                .Where(page =>
                    page.Group == LatticeSettingsGroup.Plugin &&
                    page.Plugin == plugin.Id)
                .OrderBy(page => page.Order)
                .ThenBy(page => page.Title.ToString(), StringComparer.Ordinal)
                .ToArray();
            if (pages.Length > 0)
            {
                groups.Add(new SettingsGroupNode(
                    plugin.Id.Value,
                    plugin.DisplayName,
                    pages));
            }
        }

        return groups;
    }

    private void Select(SettingsPageId pageId) => _selectedPageId = pageId;

    private bool IsCollapsed(string key) => _collapsedGroups.Contains(key);

    private void ToggleGroup(string key)
    {
        if (!_collapsedGroups.Add(key))
        {
            _collapsedGroups.Remove(key);
        }
    }

    private int? ReadInt(SettingsKey key) =>
        Settings.GetValue(key) is int value ? value : null;

    private bool ReadBool(SettingsKey key) => Settings.GetValue(key) is true;

    private void Set(SettingDescriptor setting, object? value)
    {
        var result = Settings.Set(setting.Key, value);
        _error = result.Success
            ? null
            : result.ErrorCode is { } code
                ? Localizer.GetError(code, result.ErrorMessage)
                : result.ErrorMessage;
    }

    private sealed record SettingsGroupNode(
        string Key,
        StringKey Title,
        IReadOnlyList<SettingsPageDescriptor> Pages);
}
