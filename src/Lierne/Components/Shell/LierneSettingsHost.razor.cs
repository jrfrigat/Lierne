
using Flare.Components.IDE;
using Microsoft.AspNetCore.Components;

namespace Lierne;

/// <summary>Composes the settings document from the plugin catalog and the application settings store.</summary>
public partial class LierneSettingsHost : IDisposable
{
    private IReadOnlyList<SettingsGroupNode> _groups = [];
    private ILookup<SettingsPageId?, SettingDescriptor> _settingsByPage = default!;
    private string? _selectedId;
    private string? _error;

    private string PageListLabel => Localizer.Get(new StringKey("lattice", "options"));

    private string NoSettingsText => Localizer.Get(new StringKey("lattice", "noSettings"));

    private IReadOnlyList<FlareSettingsCategory> _categories = [];
    private IReadOnlyDictionary<string, SettingsPageDescriptor> _pagesById = default!;

    private void SelectById(string id)
    {
        _selectedId = id;
        _error = null;
    }

    private static string PageKey(SettingsPageDescriptor page) => "page:" + page.Id.Value;

    private void BuildCategories()
    {
        _categories = _groups.Select(group => new FlareSettingsCategory("group:" + group.Key, Localizer.Get(group.Title))
        {
            Selectable = false,
            Children = group.Pages.Select(page => new FlareSettingsCategory(PageKey(page), Localizer.Get(page.Title))
            {
                SearchTerms = _settingsByPage[page.Id]
                    .Select(setting => Localizer.Get(setting.Label))
                    .ToArray()
            }).ToArray()
        }).ToArray();
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _groups = BuildGroups();
        _pagesById = _groups.SelectMany(group => group.Pages)
            .ToDictionary(PageKey, StringComparer.Ordinal);
        _settingsByPage = Catalog.Settings
            .OrderBy(setting => setting.Key.Value, StringComparer.Ordinal)
            .ToLookup(setting => setting.Page);
        BuildCategories();
        Localizer.Changed += OnLocalizerChanged;
    }

    /// <inheritdoc />
    public void Dispose() => Localizer.Changed -= OnLocalizerChanged;

    private void OnLocalizerChanged() => _ = InvokeAsync(() =>
    {
        BuildCategories();
        StateHasChanged();
    });

    private IReadOnlyList<SettingsGroupNode> BuildGroups()
    {
        var groups = new List<SettingsGroupNode>();
        var general = Catalog.SettingsPages
            .Where(page => page.Group == LierneSettingsGroup.General)
            .OrderBy(page => page.Order)
            .ThenBy(page => page.Title.ToString(), StringComparer.Ordinal)
            .ToArray();
        if (general.Length > 0)
        {
            groups.Add(new SettingsGroupNode(
                "general",
                LierneHostPlugin.Key("general"),
                general));
        }

        foreach (var plugin in Catalog.Plugins.OrderBy(plugin => plugin.Order))
        {
            var pages = Catalog.SettingsPages
                .Where(page => page.Group == LierneSettingsGroup.Plugin && page.Plugin == plugin.Id)
                .OrderBy(page => page.Order)
                .ThenBy(page => page.Title.ToString(), StringComparer.Ordinal)
                .ToArray();
            if (pages.Length > 0)
            {
                groups.Add(new SettingsGroupNode("plugin:" + plugin.Id.Value, plugin.DisplayName, pages));
            }
        }

        return groups;
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
