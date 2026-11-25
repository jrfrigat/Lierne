using Flare.Components;
using Microsoft.AspNetCore.Components;

namespace Lierne;

/// <summary>Renders one node of a resolved document view layout.</summary>
public partial class LierneDocumentViewLayoutHost
{
    private static readonly StringKey AddViewLabel = new("lattice", "addDocumentView");
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

    private IReadOnlyList<DocumentViewDescriptor> VisibleViews(LierneDocumentViewGroup group) =>
        group.AdditionalViews.Count == 0 ? group.Views : group.Views.Where(view => IsVisible(group, view.Id)).ToArray();

    private IReadOnlyList<DocumentViewDescriptor> AvailableViews(LierneDocumentViewGroup group) =>
        group.Views.Where(view => !IsVisible(group, view.Id)).ToArray();

    private bool IsVisible(LierneDocumentViewGroup group, DocumentViewId id) =>
        !group.AdditionalViews.Contains(id) ||
        Document.OpenedAdditionalViews?.Contains(id) == true ||
        Document.SelectedView == id ||
        (Document.GroupSelectedViews?.TryGetValue(group.Index, out var saved) == true && saved == id);

    private EventCallback<int> ViewChanged(
        LierneDocumentViewGroup group,
        IReadOnlyList<DocumentViewDescriptor> visibleViews) =>
        EventCallback.Factory.Create<int>(this, index =>
        {
            if ((uint)index < (uint)visibleViews.Count)
            {
                SelectView(group, visibleViews[index].Id);
            }
        });

    private int SelectedIndex(LierneDocumentViewGroup group, IReadOnlyList<DocumentViewDescriptor> visibleViews)
    {
        var selected = SelectedView(group);
        for (var index = 0; index < visibleViews.Count; index++)
        {
            if (visibleViews[index].Id == selected)
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

    private void SelectView(LierneDocumentViewGroup group, DocumentViewId view) =>
        Documents.SetGroupSelectedView(Document.Id, group.Index, view);

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
