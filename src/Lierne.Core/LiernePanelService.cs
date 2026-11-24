using System.Diagnostics;

namespace Lierne;

/// <summary>Owns the semantic placement of the tool panels of one workspace.</summary>
public sealed class LiernePanelService : IPanelService
{
    private readonly Dictionary<ToolWindowId, PanelPlacement> _placements = [];
    private readonly ILierneToolWindowService _tools;
    private IReadOnlyList<PanelPlacement>? _snapshot;

    /// <summary>Creates the panel service from the composed catalog.</summary>
    /// <param name="catalog">The composed plugin catalog.</param>
    /// <param name="tools">The tool-window visibility service.</param>
    public LiernePanelService(LiernePluginCatalog catalog, ILierneToolWindowService tools)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(tools);
        _tools = tools;

        var orderByRegion = new Dictionary<LierneToolRegion, int>();
        foreach (var descriptor in catalog.ToolWindows)
        {
            var order = orderByRegion.GetValueOrDefault(descriptor.DefaultRegion);
            orderByRegion[descriptor.DefaultRegion] = order + 1;
            var visible = tools.ToolWindows
                .FirstOrDefault(window => window.Id == descriptor.Id)?
                .Visible ?? descriptor.DefaultVisible;
            _placements[descriptor.Id] = new PanelPlacement(
                descriptor.Id,
                descriptor.DefaultRegion,
                Visible: visible,
                Order: order,
                Size: SizeFor(descriptor.DefaultRegion),
                SlotSize: SizeFor(descriptor.DefaultRegion));
        }
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public IReadOnlyList<PanelPlacement> Placements =>
        _snapshot ??= Array.AsReadOnly(_placements.Values
            .OrderBy(placement => placement.Region)
            .ThenBy(placement => placement.Slot)
            .ThenBy(placement => placement.Order)
            .ThenBy(placement => placement.Panel.Value, StringComparer.Ordinal)
            .ToArray());

    /// <inheritdoc />
    public PanelPlacement? Get(ToolWindowId panel) => _placements.GetValueOrDefault(panel);

    /// <inheritdoc />
    public LierneResult Show(ToolWindowId panel)
    {
        if (!_placements.TryGetValue(panel, out var placement))
        {
            return NotFound(panel);
        }

        var result = _tools.Show(panel);
        if (!result.Success)
        {
            return result;
        }

        if (!placement.Visible)
        {
            _placements[panel] = placement with { Visible = true };
            Notify();
        }

        return LierneResult.Ok;
    }

    /// <inheritdoc />
    public LierneResult Hide(ToolWindowId panel)
    {
        if (!_placements.TryGetValue(panel, out var placement))
        {
            return NotFound(panel);
        }

        var result = _tools.Hide(panel);
        if (!result.Success)
        {
            return result;
        }

        if (placement.Visible)
        {
            _placements[panel] = placement with { Visible = false };
            Notify();
        }

        return LierneResult.Ok;
    }

    /// <inheritdoc />
    public LierneResult Move(ToolWindowId panel, PanelPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        if (!_placements.TryGetValue(panel, out var current))
        {
            return NotFound(panel);
        }

        var updated = placement.Panel == panel ? placement : placement with { Panel = panel };
        if (current == updated)
        {
            return LierneResult.Ok;
        }

        _placements[panel] = updated;
        Notify();
        return LierneResult.Ok;
    }

    /// <inheritdoc />
    public LierneResult SetPinned(ToolWindowId panel, bool pinned)
    {
        if (!_placements.TryGetValue(panel, out var placement))
        {
            return NotFound(panel);
        }

        if (placement.Pinned == pinned)
        {
            return LierneResult.Ok;
        }

        _placements[panel] = placement with { Pinned = pinned };
        Notify();
        return LierneResult.Ok;
    }

    private static double SizeFor(LierneToolRegion region) =>
        region == LierneToolRegion.Bottom ? 150 : 280;

    private static LierneResult NotFound(ToolWindowId panel) =>
        LierneResult.Fail("lattice.panel.unknown", $"The panel '{panel}' is not registered.");

    private void Notify()
    {
        _snapshot = null;
        try
        {
            Changed?.Invoke();
        }
        catch (Exception exception)
        {
            Trace.TraceError("A Lierne panel observer failed: {0}", exception);
        }
    }
}
