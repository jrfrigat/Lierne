namespace Lierne;

/// <summary>Chooses which surfaces the full Lierne shell composes.</summary>
public sealed class LierneShellOptions
{
    /// <summary>Gets or sets whether the command bar is shown.</summary>
    public bool ShowCommandBar { get; init; } = true;

    /// <summary>Gets or sets whether the generated menu bar is shown.</summary>
    public bool ShowMenuBar { get; init; } = true;

    /// <summary>Gets or sets whether the ribbon is shown when no toolbar replaces it.</summary>
    public bool ShowRibbon { get; init; } = true;

    /// <summary>Gets or sets whether contributed toolbars are shown.</summary>
    public bool ShowToolbars { get; init; } = true;

    /// <summary>Gets or sets whether the command-palette button is shown in the command bar.</summary>
    public bool ShowPaletteButton { get; init; } = true;

    /// <summary>Gets or sets whether the status bar is shown.</summary>
    public bool ShowStatusBar { get; init; } = true;

    /// <summary>Gets or sets whether the command palette is hosted.</summary>
    public bool ShowCommandPalette { get; init; } = true;

    /// <summary>Gets or sets whether the notification host is shown.</summary>
    public bool ShowNotifications { get; init; } = true;

    /// <summary>Gets or sets whether the docked workspace is shown.</summary>
    public bool ShowWorkspace { get; init; } = true;

    /// <summary>Gets or sets whether the start surface is offered when the center is empty.</summary>
    public bool ShowStartPage { get; init; } = true;

    /// <summary>Gets or sets whether the host bottom surface is shown.</summary>
    public bool ShowBottomPanel { get; init; } = true;
}
