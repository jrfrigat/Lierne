using Flare.Components;
using Microsoft.AspNetCore.Components;

namespace Lierne;

/// <summary>Renders one node of a resolved document view layout.</summary>
public partial class LierneDocumentViewLayoutHost
{
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

    private LierneDocumentViewGroup? Group(int index) =>
        index >= 0 && index < Layout.Groups.Count ? Layout.Groups[index] : null;

    private LierneDocumentViewSplit? Split(int index) =>
        index >= 0 && index < Layout.Splits.Count ? Layout.Splits[index] : null;

    private static TabsPlacement ToTabsPlacement(DocumentViewTabPlacement placement) =>
        placement == DocumentViewTabPlacement.Bottom ? TabsPlacement.Bottom : TabsPlacement.Top;

    private static SplitPaneOrientation ToSplitPaneOrientation(DocumentViewSplitAxis axis) =>
        axis == DocumentViewSplitAxis.Vertical
            ? SplitPaneOrientation.Vertical
            : SplitPaneOrientation.Horizontal;

    private EventCallback<int> ViewChanged(LierneDocumentViewGroup group) =>
        EventCallback.Factory.Create<int>(this, index => SelectView(group, index));

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

    private void SplitFractionChanged(int splitIndex, double fraction) =>
        Documents.SetSplitFraction(Document.Id, splitIndex, fraction);

    private double SplitFraction(LierneDocumentViewSplit split) =>
        Document.SplitFractions is { } fractions &&
        fractions.TryGetValue(split.Index, out var saved)
            ? DocumentViewSplitLayout.ClampFraction(saved)
            : split.Fraction;

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
}
