namespace Lierne;

/// <summary>Stores the layout controlled by the host alongside its workspace documents and tools.</summary>
public sealed record LierneWorkspaceLayout
{
    /// <summary>Gets or sets the selected document instance in each dock.</summary>
    public Dictionary<LierneDockLocation, string?> Documents { get; init; } = [];

    /// <summary>Gets or sets the left panel state.</summary>
    public LiernePanelState Left { get; init; } = new(270);

    /// <summary>Gets or sets the right panel state.</summary>
    public LiernePanelState Right { get; init; } = new(280);

    /// <summary>Gets or sets the bottom panel state.</summary>
    public LiernePanelState Bottom { get; init; } = new(150);

    /// <summary>Gets or sets the semantic placements of the tool panels.</summary>
    public IReadOnlyList<PanelPlacement>? Panels { get; init; }
}

/// <summary>The stored size, expansion, and selected tool of a shell panel.</summary>
/// <param name="Size">Width or height in pixels.</param>
/// <param name="Pinned">Whether the panel stays open.</param>
/// <param name="Collapsed">Whether the region is collapsed.</param>
/// <param name="ToolKey">The selected tool window identifier, when available.</param>
public sealed record LiernePanelState(int Size, bool Pinned = true, bool Collapsed = false, string? ToolKey = null);
