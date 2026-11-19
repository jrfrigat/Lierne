using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Lattice;

public partial class LatticeShell
{
    private readonly Dictionary<LatticeToolRegion, LatticePanelState> _panels = new()
    {
        [LatticeToolRegion.Left] = new(270),
        [LatticeToolRegion.Right] = new(280),
        [LatticeToolRegion.Bottom] = new(150)
    };
    private readonly string _layoutId = $"lattice-shell-{Guid.NewGuid():N}";
    private LatticeStatePersistence? _persistence;
    private ISettingsStore? _settings;
    private bool _workspaceReady = true;
    private Func<LatticeWorkspaceLayout>? _captureWorkspace;
    private Action<Exception>? _reportWorkspaceSaveError;
    private bool _watchRequested;
    private bool _disposed;
    private string? _stateWarning;
    private IJSObjectReference? _layoutModule;
    private DotNetObjectReference<LatticeShell>? _layoutReference;

    private void InitializePersistence()
    {
        if (Services.GetService<LatticeStatePersistence>() is not { IsEnabled: true } persistence)
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
                _layoutModule = await js.InvokeAsync<IJSObjectReference>("import", "./_content/Lattice.Components/lattice.js");
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

    private void ApplyLayout(LatticeWorkspaceLayout layout)
    {
        _panels[LatticeToolRegion.Left] = CoercePanel(layout.Left, _panels[LatticeToolRegion.Left]);
        _panels[LatticeToolRegion.Right] = CoercePanel(layout.Right, _panels[LatticeToolRegion.Right]);
        _panels[LatticeToolRegion.Bottom] = CoercePanel(layout.Bottom, _panels[LatticeToolRegion.Bottom]);
        foreach (var (location, key) in layout.Documents ?? [])
        {
            if (Enum.IsDefined(location) && (key is null || FindDocument(key)?.Location == location))
            {
                SelectDocument(ToRegion(location), key);
            }
        }
    }

    private static LatticePanelState CoercePanel(LatticePanelState? saved, LatticePanelState fallback) =>
        saved is null ? fallback : saved with { Size = saved.Size is >= 120 and <= 32768 ? saved.Size : fallback.Size };

    private string PanelSize(LatticeToolRegion region) =>
        $"{_panels[region].Size.ToString(CultureInfo.InvariantCulture)}px";

    private void SetPanel(LatticeToolRegion region, LatticePanelState panel)
    {
        if (_panels[region] != panel)
        {
            _panels[region] = panel;
            SaveWorkspace();
        }
    }

    private void SetCollapsed(LatticeToolRegion region, bool collapsed) =>
        SetPanel(region, _panels[region] with { Collapsed = collapsed });

    private void SetSelectedTool(LatticeToolRegion region, string? key) =>
        SetPanel(region, _panels[region] with { ToolKey = key });

    /// <summary>Records a splitter resize reported by the interactive layout.</summary>
    [JSInvokable]
    public Task OnPanelSizesChanged(int? left, int? right, int? bottom) => InvokeAsync(() =>
    {
        var changed = UpdateSize(LatticeToolRegion.Left, left);
        changed |= UpdateSize(LatticeToolRegion.Right, right);
        changed |= UpdateSize(LatticeToolRegion.Bottom, bottom);
        if (changed && !_disposed)
        {
            SaveWorkspace();
            StateHasChanged();
        }
    });

    private bool UpdateSize(LatticeToolRegion region, int? size)
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

    private LatticeWorkspaceLayout CaptureWorkspaceLayout() => new()
    {
        Documents = _selection.ToDictionary(pair => ToLocation(pair.Key), pair => pair.Value),
        Left = _panels[LatticeToolRegion.Left],
        Right = _panels[LatticeToolRegion.Right],
        Bottom = _panels[LatticeToolRegion.Bottom]
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
        Trace.TraceWarning("Lattice state operation {0} failed: {1}", error, exception.GetType().Name);
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
