namespace Lierne;

/// <summary>The reusable settings surface: a plugin tree on the left, the selected page on the right.</summary>
public partial class LierneSettingsHost
{
    private IReadOnlyList<SettingsGroupNode> _groups = [];
    private readonly HashSet<string> _collapsedGroups = new(StringComparer.Ordinal);
    private ILookup<SettingsPageId?, SettingDescriptor> _settingsByPage = default!;
    private SettingsPageDescriptor? _selectedPage;
    private string? _error;

    private IReadOnlyList<SettingsGroupNode> Groups => _groups;

    private string PageListLabel => Localizer.Get(new StringKey("lattice", "options"));

    private string NoSettingsText => Localizer.Get(new StringKey("lattice", "noSettings"));

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _groups = BuildGroups();
        _selectedPage = _groups.Count > 0 ? _groups[0].Pages[0] : null;
        _settingsByPage = Catalog.Settings
            .OrderBy(setting => setting.Key.Value, StringComparer.Ordinal)
            .ToLookup(setting => setting.Page);
    }

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
                .Where(page =>
                    page.Group == LierneSettingsGroup.Plugin &&
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

    private void Select(SettingsPageDescriptor page)
    {
        _selectedPage = page;
        _error = null;
    }

    private bool IsCollapsed(string key) => _collapsedGroups.Contains(key);

    private void SetGroupExpanded(string key, bool expanded)
    {
        if (expanded)
        {
            _collapsedGroups.Remove(key);
        }
        else
        {
            _collapsedGroups.Add(key);
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
