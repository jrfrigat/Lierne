namespace Lierne;

/// <summary>Owns the tool windows of one host scope.</summary>
public sealed class LierneToolWindowService : ILierneToolWindowService
{
    private readonly Dictionary<ToolWindowId, LierneToolWindowInstance> _windows;
    private IReadOnlyList<LierneToolWindowInstance>? _snapshot;

    /// <summary>Creates the tool window service from the composed catalog.</summary>
    /// <param name="catalog">The composed plugin catalog.</param>
    public LierneToolWindowService(LiernePluginCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _windows = catalog.ToolWindows.ToDictionary(
            descriptor => descriptor.Id,
            descriptor => new LierneToolWindowInstance(
                descriptor.Id,
                descriptor.DefaultRegion,
                descriptor.DefaultVisible));
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public IReadOnlyList<LierneToolWindowInstance> ToolWindows =>
        _snapshot ??= Array.AsReadOnly(_windows.Values.ToArray());

    /// <inheritdoc />
    public LierneResult Show(ToolWindowId toolWindow) => SetVisible(toolWindow, true);

    /// <inheritdoc />
    public LierneResult Hide(ToolWindowId toolWindow) => SetVisible(toolWindow, false);

    /// <inheritdoc />
    public LierneResult Toggle(ToolWindowId toolWindow)
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

    private LierneResult SetVisible(ToolWindowId toolWindow, bool visible)
    {
        if (!_windows.TryGetValue(toolWindow, out var window))
        {
            return Unknown(toolWindow);
        }

        if (window.Visible == visible)
        {
            return LierneResult.Ok;
        }

        _windows[toolWindow] = window with { Visible = visible };
        _snapshot = null;
        Changed?.Invoke();
        return LierneResult.Ok;
    }

    private static LierneResult Unknown(ToolWindowId toolWindow) =>
        LierneResult.Fail(
            "lattice.toolwindow.unknown",
            $"The tool window '{toolWindow}' is not registered.");
}
