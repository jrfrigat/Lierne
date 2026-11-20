using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Lierne;

public partial class LierneShell
{
    private readonly Dictionary<LierneToolRegion, LiernePanelState> _panels = new()
    {
        [LierneToolRegion.Left] = new(270),
        [LierneToolRegion.Right] = new(280),
        [LierneToolRegion.Bottom] = new(150)
    };
    private readonly string _layoutId = $"lierne-shell-{Guid.NewGuid():N}";
    private LierneStatePersistence? _persistence;
    private ISettingsStore? _settings;
    private bool _workspaceReady = true;
    private Func<LierneWorkspaceLayout>? _captureWorkspace;
    private Action<Exception>? _reportWorkspaceSaveError;
    private bool _watchRequested;
    private bool _disposed;
    private string? _stateWarning;
    private IJSObjectReference? _layoutModule;
    private DotNetObjectReference<LierneShell>? _layoutReference;

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

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_persistence is null || _disposed)
        {
            return;
        }

        if (firstRender && !_workspaceReady)
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

            if (!_disposed)
            {
                _workspaceReady = true;
                StateHasChanged();
            }
            return;
        }

        if (_workspaceReady && !_watchRequested && Services.GetService<IJSRuntime>() is { } js)
        {
            _watchRequested = true;
            try
            {
                _layoutModule = await js.InvokeAsync<IJSObjectReference>("import", "./_content/Lierne/lierne.js");
                if (_disposed)
                {
                    await _layoutModule.DisposeAsync();
                    _layoutModule = null;
                    return;
                }
                _layoutReference = DotNetObjectReference.Create(this);
                await _layoutModule.InvokeVoidAsync("watchLayoutSizes", _layoutId, _layoutReference);
            }
            catch (JSDisconnectedException)
            {
            }
            catch (Exception exception)
            {
                ReportStateError("stateLayoutWatchFailed", exception);
            }
        }
    }

    private void ApplyLayout(LierneWorkspaceLayout layout)
    {
        _panels[LierneToolRegion.Left] = CoercePanel(layout.Left, _panels[LierneToolRegion.Left]);
        _panels[LierneToolRegion.Right] = CoercePanel(layout.Right, _panels[LierneToolRegion.Right]);
        _panels[LierneToolRegion.Bottom] = CoercePanel(layout.Bottom, _panels[LierneToolRegion.Bottom]);
        foreach (var (location, key) in layout.Documents ?? [])
        {
            if (Enum.IsDefined(location) && (key is null || FindDocument(key)?.Location == location))
            {
                SelectDocument(ToRegion(location), key);
            }
        }
    }

    private static LiernePanelState CoercePanel(LiernePanelState? saved, LiernePanelState fallback) =>
        saved is null ? fallback : saved with { Size = saved.Size is >= 120 and <= 32768 ? saved.Size : fallback.Size };

    private string PanelSize(LierneToolRegion region) =>
        $"{_panels[region].Size.ToString(CultureInfo.InvariantCulture)}px";

    private void SetPanel(LierneToolRegion region, LiernePanelState panel)
    {
        if (_panels[region] != panel)
        {
            _panels[region] = panel;
            SaveWorkspace();
        }
    }

    private void SetCollapsed(LierneToolRegion region, bool collapsed) =>
        SetPanel(region, _panels[region] with { Collapsed = collapsed });

    private void SetSelectedTool(LierneToolRegion region, string? key) =>
        SetPanel(region, _panels[region] with { ToolKey = key });

    /// <summary>Records a splitter resize reported by the interactive layout.</summary>
    [JSInvokable]
    public Task OnPanelSizesChanged(int? left, int? right, int? bottom) => InvokeAsync(() =>
    {
        var changed = UpdateSize(LierneToolRegion.Left, left);
        changed |= UpdateSize(LierneToolRegion.Right, right);
        changed |= UpdateSize(LierneToolRegion.Bottom, bottom);
        if (changed && !_disposed)
        {
            SaveWorkspace();
            StateHasChanged();
        }
    });

    private bool UpdateSize(LierneToolRegion region, int? size)
    {
        if (size is not (>= 120 and <= 32768) || size == _panels[region].Size)
        {
            return false;
        }

        _panels[region] = _panels[region] with { Size = size.Value };
        return true;
    }

    private void OnToolWindowsChanged() => _ = InvokeAsync(() =>
    {
        SaveWorkspace();
        StateHasChanged();
    });

    private void OnSettingChanged(SettingsKey key) => _ = InvokeAsync(SavePreferences);

    private void OnShortcutsChanged() => _ = InvokeAsync(() =>
    {
        SavePreferences();
        StateHasChanged();
    });

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
                _captureWorkspace ??= CaptureWorkspaceLayout,
                _reportWorkspaceSaveError ??= OnWorkspaceSaveError);
        }
    }

    private LierneWorkspaceLayout CaptureWorkspaceLayout() => new()
    {
        Documents = _selection.ToDictionary(pair => ToLocation(pair.Key), pair => pair.Value),
        Left = _panels[LierneToolRegion.Left],
        Right = _panels[LierneToolRegion.Right],
        Bottom = _panels[LierneToolRegion.Bottom]
    };

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
            _stateWarning = error;
            StateHasChanged();
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
            if (_layoutModule is not null)
            {
                await _layoutModule.InvokeVoidAsync("unwatchLayoutSizes", _layoutId);
                await _layoutModule.DisposeAsync();
            }
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or InvalidOperationException)
        {
        }
        finally
        {
            _layoutReference?.Dispose();
        }
    }
}
