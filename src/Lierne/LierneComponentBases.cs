using Microsoft.AspNetCore.Components;

namespace Lierne;

/// <summary>Base class for a plugin document component rendered by the Lierne shell.</summary>
public abstract class LierneDocumentComponent : ComponentBase
{
    /// <summary>Gets or sets the document context supplied by the shell.</summary>
    [Parameter]
    public LierneDocumentContext Context { get; set; } = null!;
}

/// <summary>Base class for a plugin tool-window component rendered by the Lierne shell.</summary>
public abstract class LierneToolWindowComponent : ComponentBase
{
    /// <summary>Gets or sets the tool-window context supplied by the shell.</summary>
    [Parameter]
    public LierneToolWindowContext Context { get; set; } = null!;
}

/// <summary>Base class for a plugin toolbar component rendered by the Lierne shell.</summary>
public abstract class LierneToolbarComponent : ComponentBase
{
    /// <summary>Gets or sets the toolbar context supplied by the shell.</summary>
    [Parameter]
    public LierneToolbarContext Context { get; set; } = null!;
}

/// <summary>Base class for a plugin status-bar component rendered by the Lierne shell.</summary>
public abstract class LierneStatusBarComponent : ComponentBase
{
    /// <summary>Gets or sets the status-bar context supplied by the shell.</summary>
    [Parameter]
    public LierneStatusBarContext Context { get; set; } = null!;
}
