namespace Lattice;

/// <summary>Owns the tool windows of one host scope.</summary>
public sealed class LatticeToolWindowService : ILatticeToolWindowService
{
    private readonly Dictionary<ToolWindowId, LatticeToolWindowInstance> _windows;
    private IReadOnlyList<LatticeToolWindowInstance>? _snapshot;

    /// <summary>Creates the tool window service from the composed catalog.</summary>
    /// <param name="catalog">The composed plugin catalog.</param>
    public LatticeToolWindowService(LatticePluginCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _windows = catalog.ToolWindows.ToDictionary(
            descriptor => descriptor.Id,
            descriptor => new LatticeToolWindowInstance(
                descriptor.Id,
                descriptor.DefaultRegion,
                descriptor.DefaultVisible));
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public IReadOnlyList<LatticeToolWindowInstance> ToolWindows =>
        _snapshot ??= Array.AsReadOnly(_windows.Values.ToArray());

    /// <inheritdoc />
    public LatticeResult Show(ToolWindowId toolWindow) => SetVisible(toolWindow, true);

    /// <inheritdoc />
    public LatticeResult Hide(ToolWindowId toolWindow) => SetVisible(toolWindow, false);

    /// <inheritdoc />
    public LatticeResult Toggle(ToolWindowId toolWindow)
    {
        if (!_windows.TryGetValue(toolWindow, out var window))
        {
            return Unknown(toolWindow);
        }

        return SetVisible(toolWindow, !window.Visible);
    }

    /// <inheritdoc />
    public bool IsVisible(ToolWindowId toolWindow) =>
        _windows.TryGetValue(toolWindow, out var window) && window.Visible;

    private LatticeResult SetVisible(ToolWindowId toolWindow, bool visible)
    {
        if (!_windows.TryGetValue(toolWindow, out var window))
        {
            return Unknown(toolWindow);
        }

        if (window.Visible == visible)
        {
            return LatticeResult.Ok;
        }

        _windows[toolWindow] = window with { Visible = visible };
        _snapshot = null;
        Changed?.Invoke();
        return LatticeResult.Ok;
    }

    private static LatticeResult Unknown(ToolWindowId toolWindow) =>
        LatticeResult.Fail(
            "lattice.toolwindow.unknown",
            $"The tool window '{toolWindow}' is not registered.");
}
