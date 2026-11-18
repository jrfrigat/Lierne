namespace Lattice;

/// <summary>Stores the layout controlled by the host alongside its workspace documents and tools.</summary>
public sealed record LatticeWorkspaceLayout
{
    /// <summary>Gets or sets the selected document instance in each dock.</summary>
    public Dictionary<LatticeDockLocation, string?> Documents { get; init; } = [];

    /// <summary>Gets or sets the left panel state.</summary>
    public LatticePanelState Left { get; init; } = new(270);

    /// <summary>Gets or sets the right panel state.</summary>
    public LatticePanelState Right { get; init; } = new(280);

    /// <summary>Gets or sets the bottom panel state.</summary>
    public LatticePanelState Bottom { get; init; } = new(150);
}

/// <summary>The stored size, expansion, and selected tool of a shell panel.</summary>
/// <param name="Size">Width or height in pixels.</param>
/// <param name="Pinned">Whether the panel stays open.</param>
/// <param name="Collapsed">Whether the region is collapsed.</param>
/// <param name="ToolKey">The selected tool window identifier, when available.</param>
public sealed record LatticePanelState(int Size, bool Pinned = true, bool Collapsed = false, string? ToolKey = null);
