namespace Lierne;

/// <summary>One resolved tab group of a document view layout.</summary>
/// <param name="Index">The group index in document view layout order.</param>
/// <param name="Views">The views of the group in display order.</param>
/// <param name="Placement">Where the group shows its tab strip.</param>
/// <param name="ShowTabs">Whether the group paints its tab strip at all.</param>
public sealed record LierneDocumentViewGroup(
    int Index,
    IReadOnlyList<DocumentViewDescriptor> Views,
    DocumentViewTabPlacement Placement = DocumentViewTabPlacement.Top,
    bool ShowTabs = true)
{
    /// <summary>Checks whether a view belongs to this group.</summary>
    /// <param name="view">The view id.</param>
    /// <returns>Whether the view is one of the group's tabs.</returns>
    public bool Contains(DocumentViewId view)
    {
        for (var index = 0; index < Views.Count; index++)
        {
            if (Views[index].Id == view)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>One resolved split of a document view layout.</summary>
/// <param name="Index">The split index in document view layout order.</param>
/// <param name="Axis">The split axis.</param>
/// <param name="Fraction">The first-branch share, already clamped to 0.05..0.95.</param>
public sealed record LierneDocumentViewSplit(
    int Index,
    DocumentViewSplitAxis Axis,
    double Fraction);

/// <summary>A node of a resolved document view layout.</summary>
public abstract record LierneDocumentViewLayoutNode;

/// <summary>A tab group node of a resolved document view layout.</summary>
/// <param name="GroupIndex">The index of the group in <see cref="LierneDocumentViewLayout.Groups"/>.</param>
public sealed record LierneDocumentViewLayoutTabs(int GroupIndex) : LierneDocumentViewLayoutNode;

/// <summary>A split node of a resolved document view layout.</summary>
/// <param name="SplitIndex">The index of the split in <see cref="LierneDocumentViewLayout.Splits"/>.</param>
/// <param name="First">The first (left or top) branch.</param>
/// <param name="Second">The second (right or bottom) branch.</param>
public sealed record LierneDocumentViewLayoutSplit(
    int SplitIndex,
    LierneDocumentViewLayoutNode First,
    LierneDocumentViewLayoutNode Second) : LierneDocumentViewLayoutNode;

/// <summary>
/// The effective layout of a document type: the declared tree with every group and split numbered,
/// the views of each group resolved and the split shares clamped.
/// </summary>
/// <param name="Root">The root node.</param>
/// <param name="Groups">The tab groups in layout order.</param>
/// <param name="Splits">The splits in layout order.</param>
public sealed record LierneDocumentViewLayout(
    LierneDocumentViewLayoutNode Root,
    IReadOnlyList<LierneDocumentViewGroup> Groups,
    IReadOnlyList<LierneDocumentViewSplit> Splits)
{
    /// <summary>Finds the group that shows a view.</summary>
    /// <param name="view">The view id.</param>
    /// <returns>The first group that contains the view, or <see langword="null"/>.</returns>
    public LierneDocumentViewGroup? GroupFor(DocumentViewId view)
    {
        for (var index = 0; index < Groups.Count; index++)
        {
            if (Groups[index].Contains(view))
            {
                return Groups[index];
            }
        }

        return null;
    }

    /// <summary>Finds the index of the group that shows a view.</summary>
    /// <param name="view">The view id.</param>
    /// <returns>The group index, or -1 when no group contains the view.</returns>
    public int GroupIndexOf(DocumentViewId view) => GroupFor(view)?.Index ?? -1;
}

/// <summary>Turns a declared document view layout into the effective groups and splits.</summary>
public static class LierneDocumentViewLayoutResolver
{
    /// <summary>Resolves a declared layout against the contributed views of its document type.</summary>
    /// <param name="layout">The declared layout, or <see langword="null"/> for one tab group over every view.</param>
    /// <param name="views">The views of the document type in catalog order.</param>
    /// <returns>
    /// The effective layout. Views the declared tree does not reference are appended to its last tab
    /// group; a tab group with a null or empty list collects every unreferenced view instead.
    /// </returns>
    public static LierneDocumentViewLayout Resolve(
        DocumentViewLayout? layout,
        IReadOnlyList<DocumentViewDescriptor> views)
    {
        ArgumentNullException.ThrowIfNull(views);
        var viewsById = new Dictionary<DocumentViewId, DocumentViewDescriptor>(views.Count);
        for (var index = 0; index < views.Count; index++)
        {
            viewsById.TryAdd(views[index].Id, views[index]);
        }

        if (layout is null)
        {
            return new LierneDocumentViewLayout(
                new LierneDocumentViewLayoutTabs(0),
                [new LierneDocumentViewGroup(0, views.ToArray())],
                []);
        }

        var explicitViews = new HashSet<DocumentViewId>();
        CollectExplicitViews(layout, explicitViews);

        var assignedViews = new HashSet<DocumentViewId>();
        var groups = new List<GroupBuilder>();
        var splits = new List<LierneDocumentViewSplit>();
        var nextGroupIndex = 0;
        var nextSplitIndex = 0;
        GroupBuilder? catchAll = null;
        GroupBuilder? lastGroup = null;

        LierneDocumentViewLayoutNode Visit(DocumentViewLayout node)
        {
            switch (node)
            {
                case DocumentViewTabsLayout tabs:
                    var builder = new GroupBuilder(nextGroupIndex++, tabs.Placement, tabs.ShowTabs);
                    groups.Add(builder);
                    lastGroup = builder;
                    if (tabs.Views is { Count: > 0 } ids)
                    {
                        foreach (var id in ids)
                        {
                            if (viewsById.TryGetValue(id, out var view) && assignedViews.Add(id))
                            {
                                builder.Views.Add(view);
                            }
                        }
                    }
                    else
                    {
                        catchAll ??= builder;
                    }

                    return new LierneDocumentViewLayoutTabs(builder.Index);

                case DocumentViewSplitLayout split:
                    var splitIndex = nextSplitIndex++;
                    var first = split.First is null ? EmptyGroup() : Visit(split.First);
                    var second = split.Second is null ? EmptyGroup() : Visit(split.Second);
                    var axis = split.Axis == DocumentViewSplitAxis.Vertical
                        ? DocumentViewSplitAxis.Vertical
                        : DocumentViewSplitAxis.Horizontal;
                    splits.Add(new LierneDocumentViewSplit(
                        splitIndex,
                        axis,
                        DocumentViewSplitLayout.ClampFraction(split.FirstFraction)));
                    return new LierneDocumentViewLayoutSplit(splitIndex, first, second);

                default:
                    return EmptyGroup();
            }
        }

        LierneDocumentViewLayoutNode EmptyGroup()
        {
            var builder = new GroupBuilder(nextGroupIndex++, DocumentViewTabPlacement.Top, showTabs: true);
            groups.Add(builder);
            lastGroup = builder;
            return new LierneDocumentViewLayoutTabs(builder.Index);
        }

        var root = Visit(layout);

        var target = catchAll ?? lastGroup;
        if (target is not null)
        {
            foreach (var view in views)
            {
                if (!explicitViews.Contains(view.Id) && !target.Contains(view.Id))
                {
                    target.Views.Add(view);
                }
            }
        }

        var resolvedGroups = new LierneDocumentViewGroup[groups.Count];
        for (var index = 0; index < groups.Count; index++)
        {
            resolvedGroups[index] = new LierneDocumentViewGroup(
                groups[index].Index,
                groups[index].Views.ToArray(),
                groups[index].Placement,
                groups[index].ShowTabs);
        }

        return new LierneDocumentViewLayout(root, resolvedGroups, splits.ToArray());
    }

    private static void CollectExplicitViews(DocumentViewLayout node, HashSet<DocumentViewId> views)
    {
        switch (node)
        {
            case DocumentViewTabsLayout { Views: { } ids }:
                foreach (var id in ids)
                {
                    views.Add(id);
                }

                break;

            case DocumentViewSplitLayout split:
                if (split.First is not null)
                {
                    CollectExplicitViews(split.First, views);
                }

                if (split.Second is not null)
                {
                    CollectExplicitViews(split.Second, views);
                }

                break;
        }
    }

    private sealed class GroupBuilder(int index, DocumentViewTabPlacement placement, bool showTabs)
    {
        public int Index { get; } = index;

        public DocumentViewTabPlacement Placement { get; } = placement;

        public bool ShowTabs { get; } = showTabs;

        public List<DocumentViewDescriptor> Views { get; } = [];

        public bool Contains(DocumentViewId view)
        {
            for (var index = 0; index < Views.Count; index++)
            {
                if (Views[index].Id == view)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
