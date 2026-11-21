using Microsoft.AspNetCore.Components;

namespace Lierne;

/// <summary>Composes the optional Lierne surfaces into one IDE shell.</summary>
public partial class LierneShell : IDisposable
{
    [Inject] private ILierneLocalizer Localizer { get; set; } = default!;

    /// <summary>Gets or sets the title of the ribbon tab used when no toolbar is contributed.</summary>
    [Parameter]
    public string RibbonTitle { get; set; } = "Home";

    /// <summary>Gets or sets the host menu bar content.</summary>
    [Parameter]
    public RenderFragment? MenuBar { get; set; }

    /// <summary>Gets or sets the host status bar content.</summary>
    [Parameter]
    public RenderFragment? StatusBar { get; set; }

    /// <summary>Gets or sets the host bottom surface.</summary>
    [Parameter]
    public RenderFragment? BottomPanel { get; set; }

    /// <summary>Gets or sets the content shown when no document is active.</summary>
    [Parameter]
    public RenderFragment? StartContent { get; set; }

    /// <summary>Replaces the visible title of each document tab.</summary>
    [Parameter]
    public RenderFragment<LierneDocumentHeaderContext>? DocumentHeaderContent { get; set; }

    /// <summary>Gets or sets which surfaces the shell composes.</summary>
    [Parameter]
    public LierneShellOptions Options { get; set; } = new();

    /// <summary>Gets or sets a host command bar that replaces <see cref="LierneCommandBar"/>.</summary>
    [Parameter]
    public RenderFragment? CommandBarContent { get; set; }

    /// <summary>Gets or sets a host workspace that replaces <see cref="LierneWorkspace"/>.</summary>
    [Parameter]
    public RenderFragment? WorkspaceContent { get; set; }

    /// <summary>Gets or sets a host command palette that replaces <see cref="LierneCommandPalette"/>.</summary>
    [Parameter]
    public RenderFragment? CommandPaletteContent { get; set; }

    /// <summary>Gets or sets a host notification surface that replaces <see cref="LierneNotificationHost"/>.</summary>
    [Parameter]
    public RenderFragment? NotificationsContent { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized() => Localizer.Changed += OnLocalizerChanged;

    /// <inheritdoc />
    public void Dispose() => Localizer.Changed -= OnLocalizerChanged;

    private void OnLocalizerChanged() => _ = InvokeAsync(StateHasChanged);
}
