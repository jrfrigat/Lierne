using System.Globalization;
using Flare.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Lierne;

/// <summary>Renders one node of a resolved document view layout.</summary>
public partial class LierneDocumentViewLayoutHost : IAsyncDisposable
{
    private ElementReference _firstPane;
    private ElementReference _secondPane;
    private IJSRuntime? _js;
    private IJSObjectReference? _module;

    [Inject]
    private ILierneDocumentService Documents { get; set; } = default!;

    [Inject]
    private ILierneCommandService Commands { get; set; } = default!;

    [Inject]
    private ILierneLocalizer Localizer { get; set; } = default!;

    [Inject]
    private LiernePluginCatalog Catalog { get; set; } = default!;

    [Inject]
    private ILierneIconResolver Icons { get; set; } = default!;

    [Inject]
    private IServiceProvider Services { get; set; } = default!;

    /// <summary>Gets or sets the document instance being rendered.</summary>
    [Parameter, EditorRequired]
    public LierneDocumentInstance Document { get; set; } = default!;

    /// <summary>Gets or sets the resolved layout that owns the node.</summary>
    [Parameter, EditorRequired]
    public LierneDocumentViewLayout Layout { get; set; } = default!;

    /// <summary>Gets or sets the node to render.</summary>
    [Parameter, EditorRequired]
    public LierneDocumentViewLayoutNode Node { get; set; } = default!;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && Node is LierneDocumentViewLayoutSplit)
        {
            await EnsureModuleAsync();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (Exception exception) when (
                exception is JSDisconnectedException or JSException or InvalidOperationException)
            {
            }

            _module = null;
        }
    }

    private LierneDocumentViewGroup? Group(int index) =>
        index >= 0 && index < Layout.Groups.Count ? Layout.Groups[index] : null;

    private LierneDocumentViewSplit? Split(int index) =>
        index >= 0 && index < Layout.Splits.Count ? Layout.Splits[index] : null;

    private static TabsPlacement ToTabsPlacement(DocumentViewTabPlacement placement) =>
        placement == DocumentViewTabPlacement.Bottom ? TabsPlacement.Bottom : TabsPlacement.Top;

    private static FlareSplitter.SplitterOrientation SplitterOrientation(DocumentViewSplitAxis axis) =>
        axis == DocumentViewSplitAxis.Vertical
            ? FlareSplitter.SplitterOrientation.Vertical
            : FlareSplitter.SplitterOrientation.Horizontal;

    private EventCallback<int> ViewChanged(LierneDocumentViewGroup group) =>
        EventCallback.Factory.Create<int>(this, index => SelectView(group, index));

    private EventCallback<string> SizeChanged(int splitIndex) =>
        EventCallback.Factory.Create<string>(this, size => OnSplitSizeChangedAsync(splitIndex, size));

    private int SelectedIndex(LierneDocumentViewGroup group)
    {
        var selected = SelectedView(group);
        for (var index = 0; index < group.Views.Count; index++)
        {
            if (group.Views[index].Id == selected)
            {
                return index;
            }
        }

        return 0;
    }

    private DocumentViewId? SelectedView(LierneDocumentViewGroup group)
    {
        if (Document.SelectedView is { } active && group.Contains(active))
        {
            return active;
        }

        if (Document.GroupSelectedViews is { } selections &&
            selections.TryGetValue(group.Index, out var saved) &&
            group.Contains(saved))
        {
            return saved;
        }

        var descriptor = Catalog.Documents.FirstOrDefault(document => document.Id == Document.TypeId);
        if (descriptor?.DefaultView is { } fallback && group.Contains(fallback))
        {
            return fallback;
        }

        return group.Views.FirstOrDefault()?.Id;
    }

    private void SelectView(LierneDocumentViewGroup group, int index)
    {
        if (index < 0 || index >= group.Views.Count)
        {
            return;
        }

        Documents.SetGroupSelectedView(Document.Id, group.Index, group.Views[index].Id);
    }

    private Dictionary<string, object?> ViewParameters(DocumentViewDescriptor view) => new()
    {
        [nameof(LierneDocumentViewComponent.Context)] = new LierneDocumentViewContext(
            Document,
            view,
            Commands,
            Documents,
            Localizer,
            Services)
    };

    private async Task OnSplitSizeChangedAsync(int splitIndex, string size)
    {
        if (_firstPane.Id is null || _secondPane.Id is null ||
            splitIndex < 0 || splitIndex >= Layout.Splits.Count)
        {
            return;
        }

        if (await EnsureModuleAsync() is not { } module)
        {
            return;
        }

        var axis = Layout.Splits[splitIndex].Axis == DocumentViewSplitAxis.Horizontal
            ? "horizontal"
            : "vertical";
        try
        {
            var sizes = await module.InvokeAsync<double[]>(
                "measureSplit",
                _firstPane,
                _secondPane,
                axis);
            if (sizes.Length < 2)
            {
                return;
            }

            var total = sizes[0] + sizes[1];
            if (!double.IsFinite(total) || total <= 0)
            {
                return;
            }

            Documents.SetSplitFraction(Document.Id, splitIndex, sizes[0] / total);
        }
        catch (Exception exception) when (
            exception is JSDisconnectedException or JSException or InvalidOperationException)
        {
        }
    }

    private async Task<IJSObjectReference?> EnsureModuleAsync()
    {
        if (_module is not null)
        {
            return _module;
        }

        _js ??= Services.GetService<IJSRuntime>();
        if (_js is null)
        {
            return null;
        }

        try
        {
            _module = await _js.InvokeAsync<IJSObjectReference>(
                "import",
                "./_content/Lierne/lierne.js");
        }
        catch (Exception exception) when (
            exception is JSDisconnectedException or JSException or InvalidOperationException)
        {
        }

        return _module;
    }

    private static string SplitVariable(int splitIndex) =>
        "--lierne-split-" + splitIndex.ToString(CultureInfo.InvariantCulture);

    private static string SplitSelector(int splitIndex) =>
        "[data-lierne-split='" + splitIndex.ToString(CultureInfo.InvariantCulture) + "']";

    private static string FirstPaneSelector(int splitIndex) =>
        "[data-lierne-split-first='" + splitIndex.ToString(CultureInfo.InvariantCulture) + "']";

    private static string SplitStyle(LierneDocumentViewSplit split)
    {
        var direction = split.Axis == DocumentViewSplitAxis.Vertical ? "column" : "row";
        return FormattableString.Invariant(
            $"display:flex;flex-direction:{direction};height:100%;min-height:0;min-width:0;{SplitVariable(split.Index)}:{split.Fraction * 100:0.####}%;");
    }

    private static string FirstPaneStyle(LierneDocumentViewSplit split) =>
        split.Axis == DocumentViewSplitAxis.Vertical
            ? FormattableString.Invariant(
                $"flex:0 0 auto;height:var({SplitVariable(split.Index)});width:100%;min-height:0;min-width:0;overflow:hidden;")
            : FormattableString.Invariant(
                $"flex:0 0 auto;width:var({SplitVariable(split.Index)});height:100%;min-height:0;min-width:0;overflow:hidden;");

    private static string SecondPaneStyle(LierneDocumentViewSplit split) =>
        split.Axis == DocumentViewSplitAxis.Vertical
            ? "flex:1 1 0;width:100%;min-height:0;min-width:0;overflow:hidden;"
            : "flex:1 1 0;height:100%;min-height:0;min-width:0;overflow:hidden;";
}
