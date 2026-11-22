using System.Diagnostics;
using Flare.Components;
using Flare.Components.IDE;
using Flare.Icons;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Lierne;

/// <summary>Hosts the dockable documents and tool panels of a Lierne workspace.</summary>
public partial class LierneWorkspace : IDisposable, IAsyncDisposable
{
    private readonly Dictionary<LierneToolRegion, LiernePanelState> _panels = new()
    {
        [LierneToolRegion.Left] = new(270),
        [LierneToolRegion.Right] = new(280),
        [LierneToolRegion.Bottom] = new(150)
    };
    private readonly Dictionary<LierneDockLocation, string?> _selection = new()
    {
        [LierneDockLocation.Main] = null,
        [LierneDockLocation.Left] = null,
        [LierneDockLocation.Right] = null
    };
    private readonly string _workspaceId = $"lierne-workspace-{Guid.NewGuid():N}";

    private static readonly FlareSvgIcon PreviewDot = new()
    {
        Data = "M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8z"
    };

    private DockNode _layout = new DockGroup("group:main", []);
    private IReadOnlyList<DockItem> _items = [];
    private IReadOnlyDictionary<string, LierneDockEntry> _entries =
        new Dictionary<string, LierneDockEntry>(StringComparer.Ordinal);
    private string? _layoutSignature;
    private string? _lastActiveId;
    private LierneDockLocation? _lastActiveLocation;
    private LierneStatePersistence? _persistence;
    private ISettingsStore? _settings;
    private Func<LierneWorkspaceLayout>? _captureWorkspace;
    private Action<Exception>? _reportWorkspaceError;
    private IJSObjectReference? _sizeModule;
    private DotNetObjectReference<LierneWorkspace>? _sizeReference;
    private double _viewportWidth;
    private double _viewportHeight;
    private IReadOnlyDictionary<string, double>? _restoredFractions;
    private HashSet<string> _visibleTools = new(StringComparer.Ordinal);
    private bool _workspaceReady = true;
    private bool _applying;
    private bool _detailedDocumentChanges;
    private bool _disposed;

    [Inject] private ILierneDocumentService Documents { get; set; } = default!;
    [Inject] private IPanelService Panels { get; set; } = default!;
    [Inject] private ILierneToolWindowService ToolWindows { get; set; } = default!;
    [Inject] private ILierneCommandService Commands { get; set; } = default!;
    [Inject] private ILierneShortcutService Shortcuts { get; set; } = default!;
    [Inject] private ILierneLocalizer Localizer { get; set; } = default!;
    [Inject] private ILierneContext State { get; set; } = default!;
    [Inject] private LiernePluginCatalog Catalog { get; set; } = default!;
    [Inject] private LierneNotificationService Notifications { get; set; } = default!;
    [Inject] private ILierneIconResolver Icons { get; set; } = default!;
    [Inject] private IServiceProvider Services { get; set; } = default!;

    /// <summary>Gets or sets the content shown when no document is open.</summary>
    [Parameter]
    public RenderFragment? StartContent { get; set; }

    /// <summary>Gets or sets the host bottom surface. A shell contribution takes precedence.</summary>
    [Parameter]
    public RenderFragment? BottomPanel { get; set; }

    /// <summary>Replaces the visible title of each document tab.</summary>
    [Parameter]
    public RenderFragment<LierneDocumentHeaderContext>? DocumentHeaderContent { get; set; }

    /// <summary>Gets or sets whether the start surface is offered when the center is empty.</summary>
    [Parameter]
    public bool ShowStartPage { get; set; } = true;

    /// <summary>Gets or sets whether the host bottom surface is shown.</summary>
    [Parameter]
    public bool ShowBottomPanel { get; set; } = true;

    /// <summary>Gets or sets the body regions the dock offers as drop targets.</summary>
    [Parameter]
    public DockDropZones DropZones { get; set; } = DockDropZones.All;

    /// <summary>Gets or sets whether Flare paints its base drop guide while a tab is dragged.</summary>
    [Parameter]
    public bool ShowDropGuide { get; set; } = true;

    private RenderFragment? StartSurface =>
        LierneShellContentResolver.Resolve(Catalog, LierneShellSlot.StartPage) is { } start
            ? RenderComponent(start.Component)
            : StartContent;

    private RenderFragment? BottomSurface =>
        LierneShellContentResolver.Resolve(Catalog, LierneShellSlot.BottomPanel) is { } bottom
            ? RenderComponent(bottom.Component)
            : BottomPanel;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Documents is LierneDocumentService service)
        {
            service.DocumentChanged += OnDocumentChanged;
            _detailedDocumentChanges = true;
        }
        else
        {
            Documents.Changed += OnDocumentsChanged;
        }

        Panels.Changed += OnPanelsChanged;
        ToolWindows.Changed += OnToolWindowsChanged;
        Localizer.Changed += OnLocalizerChanged;
        State.Changed += OnStateChanged;
        Shortcuts.Changed += OnShortcutsChanged;
        InitializePersistence();
        InitializeDocumentSelection();
        Refresh(forceLayout: true);
        _visibleTools = [.. ToolWindows.ToolWindows.Where(tool => tool.Visible).Select(tool => tool.Id.Value)];
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed)
        {
            return;
        }

        if (_persistence is not null && !_workspaceReady)
        {
            await RunStateOperationAsync(() => _persistence.RestorePreferencesAsync(), "stateLoadFailed");
            await RunStateOperationAsync(async () =>
            {
                var layout = await _persistence.RestoreWorkspaceAsync();
                InitializeDocumentSelection();
                if (layout is not null)
                {
                    ApplyLayout(layout);
                }
            }, "stateLoadFailed");

            if (_disposed)
            {
                return;
            }

            _workspaceReady = true;
            // Restored visibility is the baseline, not a command that should select a tool.
            _visibleTools = [.. ToolWindows.ToolWindows.Where(tool => tool.Visible).Select(tool => tool.Id.Value)];
            // The restored sizes, not the pre-restore fractions, define the first painted layout.
            Refresh(forceLayout: true, rebuildFractions: true);
            StateHasChanged();
            return;
        }

        if (_workspaceReady && _sizeModule is null && Services.GetService<IJSRuntime>() is { } js)
        {
            try
            {
                _sizeModule = await js.InvokeAsync<IJSObjectReference>("import", "./_content/Lierne/lierne.js");
                if (_disposed)
                {
                    await _sizeModule.DisposeAsync();
                    _sizeModule = null;
                    return;
                }

                _sizeReference = DotNetObjectReference.Create(this);
                await _sizeModule.InvokeVoidAsync("watchWorkspaceSize", _workspaceId, _sizeReference);
            }
            catch (Exception exception) when (exception is JSDisconnectedException or InvalidOperationException)
            {
            }
            catch (Exception exception)
            {
                ReportStateError("stateLayoutWatchFailed", exception);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        if (_detailedDocumentChanges && Documents is LierneDocumentService service)
        {
            service.DocumentChanged -= OnDocumentChanged;
        }
        else
        {
            Documents.Changed -= OnDocumentsChanged;
        }

        Panels.Changed -= OnPanelsChanged;
        ToolWindows.Changed -= OnToolWindowsChanged;
        Localizer.Changed -= OnLocalizerChanged;
        State.Changed -= OnStateChanged;
        Shortcuts.Changed -= OnShortcutsChanged;
        if (_settings is not null)
        {
            _settings.Changed -= OnSettingChanged;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Dispose();
        try
        {
            if (_persistence is not null)
            {
                await _persistence.FlushAsync();
            }

            if (_sizeModule is not null)
            {
                await _sizeModule.InvokeVoidAsync("unwatchWorkspaceSize", _workspaceId);
                await _sizeModule.DisposeAsync();
            }
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or InvalidOperationException)
        {
        }
        finally
        {
            _sizeReference?.Dispose();
        }
    }

    /// <summary>Records the viewport measured by the browser.</summary>
    /// <param name="width">The workspace width in pixels.</param>
    /// <param name="height">The workspace height in pixels.</param>
    [JSInvokable]
    public Task OnViewportChanged(double width, double height) => InvokeAsync(() =>
    {
        if (!_disposed && double.IsFinite(width) && double.IsFinite(height) && width > 0 && height > 0)
        {
            _viewportWidth = width;
            _viewportHeight = height;
        }
    });

    private void InitializePersistence()
    {
        if (Services.GetService<LierneStatePersistence>() is not { IsEnabled: true } persistence)
        {
            return;
        }

        _persistence = persistence;
        _workspaceReady = false;
        _settings = Services.GetRequiredService<ISettingsStore>();
        _settings.Changed += OnSettingChanged;
    }

    private void InitializeDocumentSelection()
    {
        _selection[LierneDockLocation.Main] = null;
        _selection[LierneDockLocation.Left] = null;
        _selection[LierneDockLocation.Right] = null;
        foreach (var document in Documents.Documents)
        {
            _selection[document.Location] = document.Id;
        }

        _lastActiveId = null;
        _lastActiveLocation = null;
        SyncSelection();
    }

    internal void ApplyLayout(LierneWorkspaceLayout layout)
    {
        _restoredFractions = layout.Splits;
        _panels[LierneToolRegion.Left] = CoercePanel(layout.Left, _panels[LierneToolRegion.Left]);
        _panels[LierneToolRegion.Right] = CoercePanel(layout.Right, _panels[LierneToolRegion.Right]);
        _panels[LierneToolRegion.Bottom] = CoercePanel(layout.Bottom, _panels[LierneToolRegion.Bottom]);
        foreach (var (location, key) in layout.Documents ?? [])
        {
            if (Enum.IsDefined(location) && (key is null || FindDocument(key)?.Location == location))
            {
                _selection[location] = key;
            }
        }

        if (layout.Panels is { } saved)
        {
            foreach (var placement in saved)
            {
                if (!string.IsNullOrWhiteSpace(placement.Panel.Value) &&
                    Panels.Get(placement.Panel) is not null)
                {
                    Panels.Move(placement.Panel, placement);
                }
            }
        }

        SyncSelection();
    }

    private static LiernePanelState CoercePanel(LiernePanelState? saved, LiernePanelState fallback) =>
        saved is null ? fallback : saved with { Size = saved.Size is >= 120 and <= 32768 ? saved.Size : fallback.Size };

    private void Refresh(bool forceLayout = false, bool rebuildFractions = false)
    {
        var model = BuildModel(rebuildFractions);
        var layoutChanged = forceLayout ||
            !string.Equals(model.LayoutSignature, _layoutSignature, StringComparison.Ordinal);
        if (layoutChanged)
        {
            _layout = model.Layout;
            _layoutSignature = model.LayoutSignature;
        }

        _items = model.Items.Select(item => Decorate(item, model.Entries)).ToArray();
        _entries = model.Entries;
    }

    private DockItem Decorate(DockItem item, IReadOnlyDictionary<string, LierneDockEntry> entries)
    {
        if (!entries.TryGetValue(item.Key, out var entry))
        {
            return item;
        }

        var preview = entry.Document?.Preview == true;
        return item with
        {
            Icon = TabIcon(entry.Document, entry.Panel),
            BadgeDot = preview,
            BadgeColor = preview ? FlareColor.OnSurfaceVariant : item.BadgeColor
        };
    }

    private LierneDockModel BuildModel(bool rebuildFractions = false)
    {
        var placements = Panels.Placements.ToList();
        foreach (var tool in ToolWindows.ToolWindows.Where(tool => tool.Visible))
        {
            if (placements.Any(placement => placement.Panel == tool.Id))
            {
                continue;
            }

            var descriptor = Catalog.ToolWindows.FirstOrDefault(item => item.Id == tool.Id);
            placements.Add(new PanelPlacement(
                tool.Id,
                descriptor?.DefaultRegion ?? LierneToolRegion.Left,
                Visible: true));
        }

        var sizes = _panels.ToDictionary(
            pair => pair.Key,
            pair => (double)pair.Value.Size);
        var activePanels = _panels.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToolKey);
        var hasStart = ShowStartPage && !Documents.Documents.Any(document =>
            document.Location == LierneDockLocation.Main);
        var hasBottom = ShowBottomPanel && BottomSurface is not null;
        return LierneDockLayoutAdapter.Build(
            Documents.Documents,
            Documents.Active,
            placements,
            ToolWindows.ToolWindows,
            Catalog.ToolWindows,
            activePanels,
            _selection,
            sizes,
            Localizer.Get,
            HostLabel("start"),
            HostLabel("bottomPanel"),
            hasStart,
            hasBottom,
            rebuildFractions ? null : _layout,
            rebuildFractions ? _restoredFractions : null);
    }

    private void SyncSelection()
    {
        var documents = Documents.Documents;
        foreach (var (location, key) in _selection)
        {
            if (key is not null && FindDocument(key)?.Location != location)
            {
                string? replacement = null;
                for (var index = documents.Count - 1; index >= 0; index--)
                {
                    if (documents[index].Location == location)
                    {
                        replacement = documents[index].Id;
                        break;
                    }
                }

                _selection[location] = replacement;
            }
        }

        var active = Documents.Active;
        if (active is not null && (active.Id != _lastActiveId || active.Location != _lastActiveLocation))
        {
            _selection[active.Location] = active.Id;
        }

        _lastActiveId = active?.Id;
        _lastActiveLocation = active?.Location;
    }

    private void SelectDocument(LierneDockLocation location, string? key)
    {
        if (_selection[location] != key)
        {
            _selection[location] = key;
        }
    }

    private LierneDocumentInstance? FindDocument(string key)
    {
        var documents = Documents.Documents;
        for (var index = 0; index < documents.Count; index++)
        {
            if (string.Equals(documents[index].Id, key, StringComparison.Ordinal))
            {
                return documents[index];
            }
        }

        return null;
    }

    private bool CanRequest(DockAction action, string? key, string target)
    {
        if (key is null)
        {
            return true;
        }

        if (key is LierneDockLayoutAdapter.StartKey or LierneDockLayoutAdapter.BottomKey)
        {
            return action == DockAction.Activate;
        }

        if (LierneDockLayoutAdapter.TryDocument(key, out var documentId))
        {
            return FindDocument(documentId) is not null &&
                !(action is DockAction.Move or DockAction.Split &&
                    LierneDockLayoutAdapter.DocumentRegionOf(_layout, target) is null);
        }

        if (LierneDockLayoutAdapter.TryPanel(key, out _))
        {
            return !(action is DockAction.Move or DockAction.Split &&
                LierneDockLayoutAdapter.ToolRegionOf(_layout, target) is null);
        }

        return true;
    }

    private string GroupLabel(DockGroup group)
    {
        if (group.ActiveKey is { } key)
        {
            for (var index = 0; index < _items.Count; index++)
            {
                if (string.Equals(_items[index].Key, key, StringComparison.Ordinal))
                {
                    return _items[index].Title;
                }
            }
        }

        return HostLabel("emptyPanel");
    }

    private LierneDockEntry? Entry(string key) => _entries.GetValueOrDefault(key);

    private FlareIcon? TabIcon(LierneDocumentInstance? document, PanelPlacement? panel)
    {
        string? iconKey;
        string? iconColor = null;
        if (document is not null)
        {
            var descriptor = Catalog.Documents.FirstOrDefault(item => item.Id == document.TypeId);
            iconKey = document.IconKey ?? descriptor?.IconKey;
            iconColor = document.IconColor;
        }
        else if (panel is not null)
        {
            iconKey = Catalog.ToolWindows
                .FirstOrDefault(item => item.Id == panel.Panel)?
                .IconKey;
        }
        else
        {
            return null;
        }

        var icon = Icons.Resolve(iconKey);
        return icon is not null && !string.IsNullOrWhiteSpace(iconColor)
            ? icon with { Color = FlareColor.Custom(iconColor) }
            : icon;
    }

    private RenderFragment RenderItem(DockItem item) => builder =>
    {
        if (!_entries.TryGetValue(item.Key, out var entry))
        {
            return;
        }

        switch (entry.Kind)
        {
            case LierneDockEntryKind.Document when entry.Document is { } document:
                builder.OpenComponent<LierneDocumentHost>(0);
                builder.AddAttribute(1, nameof(LierneDocumentHost.DocumentId), document.Id);
                builder.CloseComponent();
                break;
            case LierneDockEntryKind.Panel when entry.Panel is { } placement:
                var descriptor = Catalog.ToolWindows.FirstOrDefault(item => item.Id == placement.Panel);
                var tool = ToolWindows.ToolWindows.FirstOrDefault(item => item.Id == placement.Panel);
                if (descriptor is null || tool is null)
                {
                    return;
                }

                builder.OpenComponent<DynamicComponent>(0);
                builder.AddAttribute(1, nameof(DynamicComponent.Type), descriptor.Component);
                builder.AddAttribute(2, nameof(DynamicComponent.Parameters), new Dictionary<string, object?>
                {
                    [nameof(LierneToolWindowComponent.Context)] = new LierneToolWindowContext(
                        tool,
                        Commands,
                        ToolWindows,
                        Localizer,
                        Services)
                });
                builder.CloseComponent();
                break;
            case LierneDockEntryKind.Start when StartSurface is { } start:
                builder.AddContent(0, start);
                break;
            case LierneDockEntryKind.Bottom when BottomSurface is { } bottom:
                builder.AddContent(0, bottom);
                break;
        }
    };

    internal DockNode Layout => _layout;

    internal IReadOnlyList<DockItem> Items => _items;

    internal async Task ApplyChangeAsync(DockLayoutChange change)
    {
        if (!ReferenceEquals(change.Previous, _layout))
        {
            Refresh(forceLayout: true);
            return;
        }

        _applying = true;
        var changed = false;
        try
        {
            changed = change.Action switch
            {
                DockAction.Activate => Activate(change),
                DockAction.Close => Close(change),
                DockAction.Move or DockAction.Split => Move(change),
                DockAction.Resize => Resize(change),
                _ => false
            };
        }
        finally
        {
            _applying = false;
        }

        SyncSelection();
        _layout = change.Proposed;
        var model = BuildModel();
        _layoutSignature = model.LayoutSignature;
        _items = model.Items.Select(item => Decorate(item, model.Entries)).ToArray();
        _entries = model.Entries;
        if (changed)
        {
            SaveWorkspace();
        }
    }

    private bool Activate(DockLayoutChange change)
    {
        if (change.ItemKey is not { } key)
        {
            return false;
        }

        if (LierneDockLayoutAdapter.TryDocument(key, out var documentId))
        {
            var changed = Documents.Active?.Id != documentId;
            if (LierneDockLayoutAdapter.DocumentRegionOf(change.Previous, change.TargetKey) is { } location &&
                _selection[location] != documentId)
            {
                SelectDocument(location, documentId);
                changed = true;
            }

            Documents.Activate(documentId);
            return changed;
        }

        if (LierneDockLayoutAdapter.TryPanel(key, out var panel))
        {
            var region = LierneDockLayoutAdapter.ToolRegionOf(change.Previous, change.TargetKey) ??
                Panels.Get(panel)?.Region;
            if (region is { } selectedRegion && _panels[selectedRegion].ToolKey != panel.Value)
            {
                _panels[selectedRegion] = _panels[selectedRegion] with { ToolKey = panel.Value };
                return true;
            }
        }

        return false;
    }

    // A tab closes whatever it represents: a document is closed, a tool window is hidden back to
    // its rail, which is what the dock's own close action does for a panel.
    private void CloseItem(DockItem item)
    {
        if (_disposed)
        {
            return;
        }

        switch (Entry(item.Key))
        {
            case { Document: { } document }:
                Documents.Close(document.Id);
                break;

            case { Panel: { } panel }:
                Panels.Hide(panel.Panel);
                break;
        }
    }

    private bool Close(DockLayoutChange change)
    {
        if (change.ItemKey is not { } key)
        {
            return false;
        }

        if (LierneDockLayoutAdapter.TryDocument(key, out var documentId))
        {
            return Documents.Close(documentId).Success;
        }

        return LierneDockLayoutAdapter.TryPanel(key, out var panel) && Panels.Hide(panel).Success;
    }

    private bool Move(DockLayoutChange change)
    {
        if (change.ItemKey is not { } key)
        {
            return false;
        }

        if (LierneDockLayoutAdapter.TryDocument(key, out var documentId))
        {
            if (LierneDockLayoutAdapter.DocumentRegionOf(change.Previous, change.TargetKey) is not { } location ||
                FindDocument(documentId)?.Location == location)
            {
                return false;
            }

            var moved = Documents.Move(documentId, location).Success;
            SelectDocument(location, documentId);
            return moved;
        }

        if (!LierneDockLayoutAdapter.TryPanel(key, out var panel) || Panels.Get(panel) is not { } placement)
        {
            return false;
        }

        var targetRegion = LierneDockLayoutAdapter.ToolRegionOf(change.Previous, change.TargetKey);
        if (targetRegion is not { } region || region == placement.Region)
        {
            return false;
        }

        var order = Panels.Placements
            .Where(item => item.Region == region && item.Panel != panel)
            .Select(item => item.Order)
            .DefaultIfEmpty(-1)
            .Max() + 1;
        return Panels.Move(panel, placement with { Region = region, Order = order, Visible = true }).Success;
    }

    private bool Resize(DockLayoutChange change)
    {
        if (change.Proposed is not DockSplit root)
        {
            return false;
        }

        var split = FindSplit(root, change.TargetKey);
        if (split is null)
        {
            return false;
        }

        var width = _viewportWidth > 0 ? _viewportWidth : 1200;
        var height = _viewportHeight > 0 ? _viewportHeight : 800;
        return split.Key switch
        {
            "split:left" => SetSize(LierneToolRegion.Left, split.Fraction * width),
            "split:right" => SetSize(LierneToolRegion.Right, (1 - split.Fraction) * width),
            "split:bottom" => SetSize(LierneToolRegion.Bottom, (1 - split.Fraction) * height),
            _ => false
        };
    }

    private bool SetSize(LierneToolRegion region, double size)
    {
        var rounded = Math.Round(Math.Clamp(size, 120, 32768));
        if (Math.Abs(rounded - _panels[region].Size) < 1)
        {
            return false;
        }

        _panels[region] = _panels[region] with { Size = (int)rounded };
        return true;
    }

    private static DockSplit? FindSplit(DockNode? node, string key)
    {
        if (node is not DockSplit split)
        {
            return null;
        }

        return split.Key == key
            ? split
            : FindSplit(split.First, key) ?? FindSplit(split.Second, key);
    }

    private void OnDocumentChanged(LierneDocumentChange change)
    {
        if (_applying)
        {
            return;
        }

        _ = InvokeAsync(() =>
        {
            if (_disposed)
            {
                return;
            }

            if ((change & (LierneDocumentChange.Structure | LierneDocumentChange.Selection)) != 0)
            {
                SyncSelection();
            }

            Refresh();
            if ((change & LierneDocumentChange.Workspace) != 0)
            {
                SaveWorkspace();
            }

            StateHasChanged();
        });
    }

    private void OnDocumentsChanged()
    {
        if (_applying)
        {
            return;
        }

        _ = InvokeAsync(() =>
        {
            if (_disposed)
            {
                return;
            }

            SyncSelection();
            Refresh();
            SaveWorkspace();
            StateHasChanged();
        });
    }

    private void OnPanelsChanged()
    {
        if (!_applying)
        {
            _ = InvokeAsync(() =>
            {
                if (!_disposed)
                {
                    Refresh();
                    SaveWorkspace();
                    StateHasChanged();
                }
            });
        }
    }

    private void OnToolWindowsChanged()
    {
        if (!_applying)
        {
            _ = InvokeAsync(() =>
            {
                if (!_disposed)
                {
                    ActivateNewlyShownTools();
                    Refresh();
                    SaveWorkspace();
                    StateHasChanged();
                }
            });
        }
    }

    // Showing a tool from a command brings it to the front of its region, the way a View-menu
    // command opens its tool window. Restored visibility is recorded as the baseline instead, so
    // restoring the workspace keeps the saved selection.
    private void ActivateNewlyShownTools()
    {
        var visible = ToolWindows.ToolWindows.Where(tool => tool.Visible).ToArray();
        if (_workspaceReady)
        {
            foreach (var tool in visible)
            {
                if (!_visibleTools.Add(tool.Id.Value))
                {
                    continue;
                }

                var region = Panels.Get(tool.Id)?.Region ?? tool.Region;
                _panels[region] = _panels[region] with { ToolKey = tool.Id.Value };
            }
        }

        _visibleTools = [.. visible.Select(tool => tool.Id.Value)];
    }

    private void OnLocalizerChanged() => _ = InvokeAsync(() =>
    {
        if (!_disposed)
        {
            Refresh();
            StateHasChanged();
        }
    });

    private void OnStateChanged() => _ = InvokeAsync(StateHasChanged);

    private void OnShortcutsChanged() => _ = InvokeAsync(() =>
    {
        SavePreferences();
        StateHasChanged();
    });

    private void OnSettingChanged(SettingsKey key) => _ = InvokeAsync(SavePreferences);

    private void SavePreferences()
    {
        if (_workspaceReady && !_disposed && _persistence is { } persistence)
        {
            _ = RunStateOperationAsync(() => persistence.SavePreferencesAsync(), "stateSaveFailed");
        }
    }

    private void SaveWorkspace()
    {
        if (_workspaceReady && !_disposed && _persistence is { } persistence)
        {
            persistence.QueueWorkspaceSave(
                _captureWorkspace ??= CaptureWorkspace,
                _reportWorkspaceError ??= OnWorkspaceSaveError);
        }
    }

    private LierneWorkspaceLayout CaptureWorkspace() => new()
    {
        Documents = new Dictionary<LierneDockLocation, string?>(_selection),
        Left = _panels[LierneToolRegion.Left],
        Right = _panels[LierneToolRegion.Right],
        Bottom = _panels[LierneToolRegion.Bottom],
        Panels = ToolWindows.ToolWindows
            .Select(tool => Panels.Get(tool.Id) is { } placement
                ? placement with { Visible = tool.Visible }
                : null)
            .Where(placement => placement is not null)
            .Cast<PanelPlacement>()
            .ToArray(),
        Splits = CaptureFractions(_layout)
    };

    private static Dictionary<string, double> CaptureFractions(
        DockNode? node,
        Dictionary<string, double>? fractions = null)
    {
        fractions ??= new Dictionary<string, double>(StringComparer.Ordinal);
        if (node is DockSplit split)
        {
            fractions[split.Key] = split.Fraction;
            CaptureFractions(split.First, fractions);
            CaptureFractions(split.Second, fractions);
        }

        return fractions;
    }

    private void OnWorkspaceSaveError(Exception exception)
    {
        if (exception is not JSDisconnectedException)
        {
            ReportStateError("stateSaveFailed", exception);
        }
    }

    private async Task RunStateOperationAsync(Func<Task> operation, string error)
    {
        try
        {
            await operation();
        }
        catch (JSDisconnectedException)
        {
        }
        catch (Exception exception)
        {
            ReportStateError(error, exception);
        }
    }

    private void ReportStateError(string error, Exception exception)
    {
        Trace.TraceWarning("Lierne state operation {0} failed: {1}", error, exception.GetType().Name);
        if (!_disposed)
        {
            Notifications.Notify(LierneNotification.Warning(HostKey(error)));
        }
    }

    private static RenderFragment RenderComponent(Type component) => builder =>
    {
        builder.OpenComponent<DynamicComponent>(0);
        builder.AddAttribute(1, nameof(DynamicComponent.Type), component);
        builder.CloseComponent();
    };

    private StringKey HostKey(string name) => new("lattice", name);

    private string HostLabel(string name) => Localizer.Get(HostKey(name));
}
