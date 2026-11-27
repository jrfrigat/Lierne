
using Microsoft.AspNetCore.Components;
#if LIERNE_FLARE_SETTINGS
using Flare.Components.IDE;
#endif

namespace Lierne;

/// <summary>Composes the settings document from the plugin catalog and the application settings store.</summary>
public partial class LierneSettingsHost : IDisposable
{
    private IReadOnlyList<SettingsGroupNode> _groups = [];
    private readonly HashSet<string> _collapsedGroups = new(StringComparer.Ordinal);
    private ILookup<SettingsPageId?, SettingDescriptor> _settingsByPage = default!;
    private SettingsPageDescriptor? _selectedPage;
    private string? _error;

    private string PageListLabel => Localizer.Get(new StringKey("lattice", "options"));

    private string NoSettingsText => Localizer.Get(new StringKey("lattice", "noSettings"));

#if LIERNE_FLARE_SETTINGS
    private IReadOnlyList<FlareSettingsCategory> _categories = [];
    private IReadOnlyDictionary<string, SettingsPageDescriptor> _pagesById =
        new Dictionary<string, SettingsPageDescriptor>(StringComparer.Ordinal);

    private RenderFragment? SettingsSurface => builder =>
    {
        builder.OpenComponent<FlareSettings>(0);
        builder.AddAttribute(1, nameof(FlareSettings.Categories), _categories);
        builder.AddAttribute(2, nameof(FlareSettings.SelectedId), _selectedPage is { } page ? PageKey(page) : null);
        builder.AddAttribute(3, nameof(FlareSettings.SelectedIdChanged),
            EventCallback.Factory.Create<string>(this, SelectById));
        builder.AddAttribute(4, nameof(FlareSettings.NavigationLabel), PageListLabel);
        builder.AddAttribute(5, nameof(FlareSettings.PageContent),
            (RenderFragment<FlareSettingsCategory>)(category => PageContent(_pagesById[category.Id])));
        builder.AddAttribute(6, nameof(FlareSettings.EmptyContent), EmptyPage);
        builder.CloseComponent();
    };

    private void SelectById(string id)
    {
        if (_pagesById.TryGetValue(id, out var page))
        {
            Select(page);
        }
    }

    private static string PageKey(SettingsPageDescriptor page) => "page:" + page.Id.Value;

    private void BuildCategories()
    {
        _pagesById = _groups.SelectMany(group => group.Pages)
            .ToDictionary(PageKey, StringComparer.Ordinal);
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
#else
    // Published Flare packages predating the settings surface still use the same page renderer.
    private static RenderFragment? SettingsSurface => null;
#endif

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _groups = BuildGroups();
        _selectedPage = _groups.Count > 0 ? _groups[0].Pages[0] : null;
        _settingsByPage = Catalog.Settings
            .OrderBy(setting => setting.Key.Value, StringComparer.Ordinal)
            .ToLookup(setting => setting.Page);
#if LIERNE_FLARE_SETTINGS
        BuildCategories();
#endif
        Localizer.Changed += OnLocalizerChanged;
    }

    /// <inheritdoc />
    public void Dispose() => Localizer.Changed -= OnLocalizerChanged;

    private void OnLocalizerChanged() => _ = InvokeAsync(() =>
    {
#if LIERNE_FLARE_SETTINGS
        BuildCategories();
#endif
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
                groups.Add(new SettingsGroupNode(plugin.Id.Value, plugin.DisplayName, pages));
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
