namespace Lierne;

/// <summary>Chooses the axis of a document view split.</summary>
public enum DocumentViewSplitAxis
{
    /// <summary>The first layout is left of the second; the splitter resizes widths.</summary>
    Horizontal,

    /// <summary>The first layout is above the second; the splitter resizes heights.</summary>
    Vertical
}

/// <summary>Chooses where a tab group shows its tab strip.</summary>
public enum DocumentViewTabPlacement
{
    /// <summary>The tab strip is above the views. The default.</summary>
    Top,

    /// <summary>The tab strip is below the views, for example for a results group.</summary>
    Bottom
}

/// <summary>
/// Describes how a document type arranges the views contributed to it. A descriptor without a layout
/// shows every view in one tab strip; a layout can place tab groups above, below or beside each other.
/// </summary>
public abstract record DocumentViewLayout;

/// <summary>
/// A tab group. The explicit list defines the tabs and their order; a null or empty list collects every
/// view of the document type that no other part of the layout references, in catalog order.
/// </summary>
/// <param name="Views">The views of the group, or <see langword="null"/> to collect the unreferenced views.</param>
public sealed record DocumentViewTabsLayout(IReadOnlyList<DocumentViewId>? Views = null)
    : DocumentViewLayout
{
    /// <summary>Gets where the group shows its tab strip. The default is <see cref="DocumentViewTabPlacement.Top"/>.</summary>
    public DocumentViewTabPlacement Placement { get; init; } = DocumentViewTabPlacement.Top;

    /// <summary>
    /// Gets whether the group paints its tab strip. Set to <see langword="false"/> for a group whose
    /// single view should fill the branch without chrome; a group with several views then shows the
    /// selected view only. The default is <see langword="true"/>.
    /// </summary>
    public bool ShowTabs { get; init; } = true;
}

/// <summary>
/// A split between two layouts. The first branch takes <see cref="FirstFraction"/> of the split axis;
/// the effective value is clamped to <see cref="MinimumFraction"/>..<see cref="MaximumFraction"/>.
/// </summary>
/// <param name="First">The layout of the first (left or top) branch.</param>
/// <param name="Second">The layout of the second (right or bottom) branch.</param>
/// <param name="Axis">The split axis.</param>
/// <param name="FirstFraction">The initial share of the first branch, from 0.05 to 0.95.</param>
public sealed record DocumentViewSplitLayout(
    DocumentViewLayout First,
    DocumentViewLayout Second,
    DocumentViewSplitAxis Axis = DocumentViewSplitAxis.Horizontal,
    double FirstFraction = 0.5)
    : DocumentViewLayout
{
    /// <summary>The smallest share the first branch may take.</summary>
    public const double MinimumFraction = 0.05;

    /// <summary>The largest share the first branch may take.</summary>
    public const double MaximumFraction = 0.95;

    /// <summary>Clamps a first-branch share to the accepted range.</summary>
    /// <param name="fraction">The share to clamp.</param>
    /// <returns>The share within 0.05..0.95, or 0.5 when it is not a finite number.</returns>
    public static double ClampFraction(double fraction) =>
        double.IsFinite(fraction)
            ? Math.Clamp(fraction, MinimumFraction, MaximumFraction)
            : 0.5;
}
