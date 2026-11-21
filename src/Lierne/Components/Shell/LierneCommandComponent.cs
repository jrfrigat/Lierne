using Flare.Components;
using Flare.Icons;
using Microsoft.AspNetCore.Components;

namespace Lierne;

/// <summary>Shares command presentation, execution and refresh behavior between shell surfaces.</summary>
public abstract class LierneCommandComponent : ComponentBase, IDisposable
{
    private CommandPresentation _presentation = null!;
    private bool _disposed;
    private int _refresh;

    [Inject] private ICommandPresentationService Presentations { get; set; } = default!;
    [Inject] private ILierneCommandService Commands { get; set; } = default!;
    [Inject] private ILierneShortcutService Shortcuts { get; set; } = default!;
    [Inject] private ILierneLocalizer Localizer { get; set; } = default!;
    [Inject] private ILierneIconResolver Icons { get; set; } = default!;
    [Inject] private ILierneContext State { get; set; } = default!;
    [Inject] private ILierneDocumentService Documents { get; set; } = default!;
    [Inject] private ILierneToolWindowService ToolWindows { get; set; } = default!;
    [Inject] private IPanelService Panels { get; set; } = default!;

    /// <summary>Gets or sets the command shown by the surface element.</summary>
    [Parameter, EditorRequired]
    public CommandId Command { get; set; }

    /// <summary>Gets or sets the placement that overrides the command metadata on this surface.</summary>
    [Parameter]
    public CommandPlacementDescriptor? Placement { get; set; }

    /// <summary>Handles command execution and failure feedback in the enclosing surface.</summary>
    [Parameter]
    public EventCallback<CommandId> ExecuteCommand { get; set; }

    /// <summary>Gets the current presentation of the command.</summary>
    protected CommandPresentation Presentation => _presentation;

    /// <summary>Gets the localized title, taking the placement override into account.</summary>
    protected string Title => Localizer.Get(Placement?.TitleOverride ?? _presentation.Title);

    /// <summary>Gets the resolved icon, taking the placement override into account.</summary>
    protected FlareIcon? Icon => Icons.Resolve(Placement?.IconOverride ?? _presentation.IconKey);

    /// <summary>Gets the tooltip with the description and shortcut, when they exist.</summary>
    protected string Tooltip
    {
        get
        {
            var title = Title;
            var description = DescriptionKey is { } descriptionKey
                ? Localizer.Get(descriptionKey)
                : null;
            var shortcut = _presentation.ShortcutText;
            return string.IsNullOrWhiteSpace(description)
                ? string.IsNullOrWhiteSpace(shortcut) ? title : $"{title} ({shortcut})"
                : string.IsNullOrWhiteSpace(shortcut)
                    ? $"{title} - {description}"
                    : $"{title} - {description} ({shortcut})";
        }
    }

    /// <summary>Gets the shortcut text of the command, when it has one.</summary>
    protected string? ShortcutText => _presentation.ShortcutText;

    private StringKey? DescriptionKey => Placement?.DescriptionOverride ?? _presentation.Description;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _presentation = Presentations.GetPresentation(Command);
        Presentations.Changed += OnCommandChanged;
        Localizer.Changed += OnChanged;
        Shortcuts.Changed += OnChanged;
        State.Changed += OnChanged;
        Documents.Changed += OnChanged;
        ToolWindows.Changed += OnChanged;
        Panels.Changed += OnChanged;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (_presentation.Command != Command)
        {
            _presentation = Presentations.GetPresentation(Command);
        }

        _ = RefreshAsync();
    }

    /// <inheritdoc />
    public virtual void Dispose()
    {
        _disposed = true;
        Presentations.Changed -= OnCommandChanged;
        Localizer.Changed -= OnChanged;
        Shortcuts.Changed -= OnChanged;
        State.Changed -= OnChanged;
        Documents.Changed -= OnChanged;
        ToolWindows.Changed -= OnChanged;
        Panels.Changed -= OnChanged;
    }

    private void OnCommandChanged(CommandId command)
    {
        if (command == Command)
        {
            Refresh();
        }
    }

    private void OnChanged()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        _presentation = Presentations.GetPresentation(Command);
        _ = InvokeAsync(StateHasChanged);
    }

    private async Task RefreshAsync()
    {
        var version = ++_refresh;
        try
        {
            var presentation = await Presentations.UpdateAsync(Command);
            if (!_disposed && version == _refresh)
            {
                _presentation = presentation;
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceWarning(
                "Could not update the presentation of {0}: {1}", Command, exception.GetType().Name);
        }
    }

    /// <summary>Executes the command through the enclosing surface or the command service.</summary>
    /// <returns>The execution task.</returns>
    protected Task ExecuteAsync()
    {
        if (ExecuteCommand.HasDelegate)
        {
            return ExecuteCommand.InvokeAsync(Command);
        }

        return Commands.ExecuteAsync(Command);
    }
}
