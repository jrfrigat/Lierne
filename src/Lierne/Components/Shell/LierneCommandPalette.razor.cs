using Flare.Components;
using Flare.Icons;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Lierne;

/// <summary>Shows the searchable command palette of the shell.</summary>
public partial class LierneCommandPalette : IDisposable
{
    private IReadOnlyList<CommandDescriptor> _results = [];
    private readonly Dictionary<CommandId, CommandPlacementDescriptor> _placements = [];
    private string _query = string.Empty;
    private int _selected;
    private bool _open;
    private bool _disposed;

    [Inject] private LiernePaletteService Palette { get; set; } = default!;
    [Inject] private ICommandPlacementService Placements { get; set; } = default!;
    [Inject] private ICommandPresentationService Presentations { get; set; } = default!;
    [Inject] private ILierneCommandService Commands { get; set; } = default!;
    [Inject] private ILierneLocalizer Localizer { get; set; } = default!;
    [Inject] private ILierneIconResolver Icons { get; set; } = default!;
    [Inject] private LierneNotificationService Notifications { get; set; } = default!;

    private string Title => Localizer.Get(LierneHostPlugin.Key("commandPalette"));

    private string Placeholder => Localizer.Get(LierneHostPlugin.Key("commandPalettePlaceholder"));

    private string EmptyText => Localizer.Get(LierneHostPlugin.Key("commandPaletteEmpty"));

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Palette.Changed += OnPaletteChanged;
        Localizer.Changed += OnLocalizerChanged;
        foreach (var placement in Placements.ForSurface(CommandSurface.Palette))
        {
            _placements.TryAdd(placement.Command, placement);
        }

        _open = Palette.IsOpen;
        if (_open)
        {
            RefreshResults();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        Palette.Changed -= OnPaletteChanged;
        Localizer.Changed -= OnLocalizerChanged;
    }

    private void OnPaletteChanged()
    {
        if (_disposed)
        {
            return;
        }

        _open = Palette.IsOpen;
        if (_open)
        {
            _query = string.Empty;
            _selected = 0;
            RefreshResults();
        }

        _ = InvokeAsync(StateHasChanged);
    }

    private void OnLocalizerChanged() => _ = InvokeAsync(StateHasChanged);

    private void OnQueryChanged(string? value)
    {
        _query = value ?? string.Empty;
        _selected = 0;
        RefreshResults();
    }

    private void RefreshResults()
    {
        _results = Presentations.Search(_query)
            .Where(command => command.ArgumentType is null &&
                Presentations.GetPresentation(command.Id).Visible)
            .ToArray();
        if (_selected >= _results.Count)
        {
            _selected = Math.Max(0, _results.Count - 1);
        }
    }

    private void Close() => Palette.Close();

    private void BackdropClick() => Close();

    private async Task HandleKeyDownAsync(KeyboardEventArgs args)
    {
        switch (args.Key)
        {
            case "Escape":
                Close();
                return;
            case "ArrowDown" when _results.Count > 0:
                _selected = (_selected + 1) % _results.Count;
                return;
            case "ArrowUp" when _results.Count > 0:
                _selected = (_selected - 1 + _results.Count) % _results.Count;
                return;
            case "Enter" when _results.Count > 0:
                await ExecuteAsync(_results[_selected]);
                return;
        }
    }

    internal async Task ExecuteAsync(CommandDescriptor descriptor)
    {
        var presentation = Presentations.GetPresentation(descriptor.Id);
        if (!presentation.Enabled)
        {
            return;
        }

        Close();
        var result = await Commands.ExecuteAsync(descriptor.Id);
        if (!result.Success)
        {
            Notifications.NotifyError(result.ErrorCode, result.ErrorMessage);
        }
    }

    private CommandPresentation Presentation(CommandDescriptor descriptor) =>
        Presentations.GetPresentation(descriptor.Id);

    private StringKey TitleOf(CommandDescriptor descriptor, CommandPresentation presentation) =>
        _placements.TryGetValue(descriptor.Id, out var placement) && placement.TitleOverride is { } title
            ? title
            : presentation.Title;

    private IReadOnlyDictionary<string, object> InputAttributes => new Dictionary<string, object>
    {
        ["aria-label"] = Title,
        ["autocomplete"] = "off"
    };

    private FlareIcon? IconOf(CommandDescriptor descriptor, CommandPresentation presentation)
    {
        var iconKey = _placements.TryGetValue(descriptor.Id, out var placement) &&
            placement.IconOverride is { } overrideKey
            ? overrideKey
            : presentation.IconKey;
        return Icons.Resolve(iconKey);
    }

    private string? DescriptionOf(CommandPresentation presentation) =>
        presentation.Description is { } description ? Localizer.Get(description) : null;
}
