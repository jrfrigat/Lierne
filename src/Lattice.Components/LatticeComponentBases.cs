using Microsoft.AspNetCore.Components;

namespace Lattice;

/// <summary>Base class for a plugin document component rendered by the Lattice shell.</summary>
public abstract class LatticeDocumentComponent : ComponentBase
{
    /// <summary>Gets or sets the document context supplied by the shell.</summary>
    [Parameter]
    public LatticeDocumentContext Context { get; set; } = null!;
}

/// <summary>Base class for a plugin tool-window component rendered by the Lattice shell.</summary>
public abstract class LatticeToolWindowComponent : ComponentBase
{
    /// <summary>Gets or sets the tool-window context supplied by the shell.</summary>
    [Parameter]
    public LatticeToolWindowContext Context { get; set; } = null!;
}

/// <summary>Base class for a plugin toolbar component rendered by the Lattice shell.</summary>
public abstract class LatticeToolbarComponent : ComponentBase
{
    /// <summary>Gets or sets the toolbar context supplied by the shell.</summary>
    [Parameter]
    public LatticeToolbarContext Context { get; set; } = null!;
}
