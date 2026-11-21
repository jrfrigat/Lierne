namespace Lierne;

/// <summary>Chooses which half of a tool region hosts a panel.</summary>
public enum PanelSlot
{
    /// <summary>The primary slot of the region.</summary>
    Primary,

    /// <summary>The secondary slot of the region.</summary>
    Secondary
}

/// <summary>Chooses how a panel is presented.</summary>
public enum PanelMode
{
    /// <summary>The panel is docked in its region.</summary>
    Docked,

    /// <summary>The panel slides out from its rail.</summary>
    AutoHide,

    /// <summary>The panel floats above the workspace.</summary>
    Floating,

    /// <summary>The panel is a separate window.</summary>
    Windowed
}

/// <summary>Describes the placement and visibility of one tool panel.</summary>
/// <param name="Panel">The tool window id.</param>
/// <param name="Region">The region that hosts the panel.</param>
/// <param name="Slot">The half of the region that hosts the panel.</param>
/// <param name="Mode">How the panel is presented.</param>
/// <param name="Visible">Whether the panel is visible.</param>
/// <param name="Pinned">Whether the panel stays open.</param>
/// <param name="Order">The order of the panel among the panels of its region and slot.</param>
/// <param name="Size">The width or height in pixels.</param>
/// <param name="SlotSize">The size of the region slot in pixels.</param>
public sealed record PanelPlacement(
    ToolWindowId Panel,
    LierneToolRegion Region,
    PanelSlot Slot = PanelSlot.Primary,
    PanelMode Mode = PanelMode.Docked,
    bool Visible = true,
    bool Pinned = true,
    int Order = 0,
    double Size = 280,
    double SlotSize = 280);

/// <summary>Owns the semantic placement of the tool panels of one workspace.</summary>
public interface IPanelService
{
    /// <summary>Raised after a panel is shown, hidden, moved or pinned.</summary>
    event Action? Changed;

    /// <summary>Gets the panel placements in composition order.</summary>
    IReadOnlyList<PanelPlacement> Placements { get; }

    /// <summary>Gets the placement of one panel.</summary>
    /// <param name="panel">The panel id.</param>
    /// <returns>The placement, or <see langword="null"/> when the panel is not registered.</returns>
    PanelPlacement? Get(ToolWindowId panel);

    /// <summary>Shows a panel.</summary>
    /// <param name="panel">The panel id.</param>
    /// <returns>Whether the panel exists.</returns>
    LierneResult Show(ToolWindowId panel);

    /// <summary>Hides a panel.</summary>
    /// <param name="panel">The panel id.</param>
    /// <returns>Whether the panel exists.</returns>
    LierneResult Hide(ToolWindowId panel);

    /// <summary>Replaces the placement of a panel.</summary>
    /// <param name="panel">The panel id.</param>
    /// <param name="placement">The new placement.</param>
    /// <returns>Whether the panel exists.</returns>
    LierneResult Move(ToolWindowId panel, PanelPlacement placement);

    /// <summary>Pins or unpins a panel.</summary>
    /// <param name="panel">The panel id.</param>
    /// <param name="pinned">Whether the panel is pinned.</param>
    /// <returns>Whether the panel exists.</returns>
    LierneResult SetPinned(ToolWindowId panel, bool pinned);
}
