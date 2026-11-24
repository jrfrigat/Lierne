using System.Collections.Immutable;
using Flare.Components.IDE;

namespace Lierne;

/// <summary>Identifies what a dock item represents.</summary>
internal enum LierneDockEntryKind
{
    Document,
    Panel,
    Start,
    Bottom
}

/// <summary>Maps one dock key back to the Lierne object that owns it.</summary>
internal sealed class LierneDockEntry(
    LierneDockEntryKind kind,
    LierneDocumentInstance? document = null,
    PanelPlacement? panel = null)
{
    public LierneDockEntryKind Kind { get; } = kind;
    public int ItemIndex { get; set; }
    public LierneDocumentInstance? Document { get; set; } = document;
    public PanelPlacement? Panel { get; } = panel;
}

/// <summary>The dock topology and catalog built from the semantic Lierne workspace.</summary>
internal sealed record LierneDockModel(
    IReadOnlyList<DockItem> Items,
    DockNode Layout,
    Dictionary<string, LierneDockEntry> Entries);

/// <summary>Builds the generic Flare dock model from documents, panels and host surfaces.</summary>
internal static class LierneDockLayoutAdapter
{
    internal const string StartKey = "host:start";
    internal const string BottomKey = "host:bottom";

    internal static LierneDockModel Build(
        IReadOnlyList<LierneDocumentInstance> documents,
        LierneDocumentInstance? activeDocument,
        IReadOnlyList<PanelPlacement> placements,
        IReadOnlyList<LierneToolWindowInstance> tools,
        IReadOnlyList<ToolWindowDescriptor> descriptors,
        IReadOnlyDictionary<LierneToolRegion, LiernePanelState> panelStates,
        IReadOnlyDictionary<LierneDockLocation, string?> selectedDocuments,
        Func<StringKey, string> localize,
        string startTitle,
        string bottomTitle,
        bool showStartPage,
        bool hasBottom,
        DockNode? current = null,
        IReadOnlyDictionary<string, double>? fractions = null)
    {
        var visible = new HashSet<ToolWindowId>();
        for (var index = 0; index < tools.Count; index++)
        {
            if (tools[index].Visible)
            {
                visible.Add(tools[index].Id);
            }
        }

        var panelOrder = placements
            .Where(placement => visible.Contains(placement.Panel))
            .OrderBy(placement => placement.Region)
            .ThenBy(placement => placement.Slot)
            .ThenBy(placement => placement.Order)
            .ThenBy(placement => placement.Panel.Value, StringComparer.Ordinal)
            .ToArray();
        var descriptorById = descriptors.ToDictionary(descriptor => descriptor.Id);
        var capacity = documents.Count + panelOrder.Length + (showStartPage ? 1 : 0) + (hasBottom ? 1 : 0);
        var entries = new Dictionary<string, LierneDockEntry>(capacity, StringComparer.Ordinal);
        var items = new List<DockItem>(capacity);
        var panelKeys = new Dictionary<LierneToolRegion, List<string>>();
        foreach (var placement in panelOrder)
        {
            var key = PanelKey(placement.Panel);
            entries[key] = new LierneDockEntry(LierneDockEntryKind.Panel, panel: placement);
            var title = descriptorById.TryGetValue(placement.Panel, out var descriptor)
                ? localize(descriptor.Title)
                : placement.Panel.Value;
            items.Add(new DockItem(key, title, KeepAlive: true, CanClose: true));
            if (!panelKeys.TryGetValue(placement.Region, out var list))
            {
                panelKeys[placement.Region] = list = [];
            }

            list.Add(key);
        }

        var documentKeys = new Dictionary<LierneDockLocation, List<string>>();
        foreach (var document in documents)
        {
            var key = DocumentKey(document.Id);
            entries[key] = new LierneDockEntry(LierneDockEntryKind.Document, document: document);
            items.Add(new DockItem(key, localize(document.Title), KeepAlive: true, CanClose: true));
            if (!documentKeys.TryGetValue(document.Location, out var list))
            {
                documentKeys[document.Location] = list = [];
            }

            list.Add(key);
        }

        var hasStart = showStartPage && !documentKeys.ContainsKey(LierneDockLocation.Main);
        if (hasStart)
        {
            entries[StartKey] = new LierneDockEntry(LierneDockEntryKind.Start);
            items.Add(new DockItem(StartKey, startTitle, KeepAlive: true, CanClose: false));
        }

        if (hasBottom)
        {
            entries[BottomKey] = new LierneDockEntry(LierneDockEntryKind.Bottom);
            items.Add(new DockItem(BottomKey, bottomTitle, KeepAlive: true, CanClose: false));
        }

        string? SelectedDocument(LierneDockLocation location) => activeDocument?.Location == location
            ? activeDocument.Id
            : selectedDocuments.GetValueOrDefault(location);
        var main = hasStart
            ? new DockGroup("group:main", [StartKey], StartKey)
            : BuildGroup("group:main", documentKeys.GetValueOrDefault(LierneDockLocation.Main),
                SelectedDocument(LierneDockLocation.Main), PrefixLength) ?? new DockGroup("group:main", []);
        var left = BuildRegion(LierneToolRegion.Left, documentKeys, panelKeys, panelStates,
            SelectedDocument(LierneDockLocation.Left));
        var right = BuildRegion(LierneToolRegion.Right, documentKeys, panelKeys, panelStates,
            SelectedDocument(LierneDockLocation.Right));
        var bottom = BuildBottom(BuildGroup("group:bottom.panels",
            panelKeys.GetValueOrDefault(LierneToolRegion.Bottom),
            panelStates.GetValueOrDefault(LierneToolRegion.Bottom)?.ToolKey, PanelPrefixLength), hasBottom);

        DockNode layout = main;
        var mainWidth = 800d;
        var leftWidth = Size(panelStates, LierneToolRegion.Left);
        var rightWidth = Size(panelStates, LierneToolRegion.Right);
        if (right is not null)
        {
            layout = new DockSplit("split:right", DockAxis.Horizontal, layout, right,
                Fraction(mainWidth / (mainWidth + rightWidth)));
        }

        if (left is not null)
        {
            layout = new DockSplit("split:left", DockAxis.Horizontal, left, layout,
                Fraction(leftWidth / (leftWidth + mainWidth + rightWidth)));
        }

        if (bottom is not null)
        {
            var bottomHeight = Size(panelStates, LierneToolRegion.Bottom);
            layout = new DockSplit("split:bottom", DockAxis.Vertical, layout, bottom,
                Fraction(600 / (600 + bottomHeight)));
        }

        // A rebuild caused by a document change keeps the splits the user has already resized:
        // recomputing every fraction from the panel sizes would snap a dragged panel back. A restored
        // workspace supplies the fractions it saved instead.
        layout = PreserveFractions(current, fractions, layout);

        return new LierneDockModel(items, layout, entries);
    }

    internal static LierneDockLocation? DocumentRegionOf(DockNode root, string targetKey) =>
        FindDocumentRegion(root, targetKey, null);

    internal static LierneToolRegion? ToolRegionOf(DockNode root, string targetKey) =>
        FindToolRegion(root, targetKey, null);

    private static LierneDockLocation? FindDocumentRegion(
        DockNode node,
        string targetKey,
        LierneDockLocation? region)
    {
        if (node is DockGroup group)
        {
            return group.Key == targetKey ? DocumentRegionOfGroup(group.Key) ?? region : null;
        }

        var split = (DockSplit)node;
        var (first, second) = DocumentRegions(split.Key, region);
        return FindDocumentRegion(split.First, targetKey, first) ??
            FindDocumentRegion(split.Second, targetKey, second);
    }

    private static LierneToolRegion? FindToolRegion(
        DockNode node,
        string targetKey,
        LierneToolRegion? region)
    {
        if (node is DockGroup group)
        {
            return group.Key == targetKey ? ToolRegionOfGroup(group.Key) ?? region : null;
        }

        var split = (DockSplit)node;
        var (first, second) = ToolRegions(split.Key, region);
        return FindToolRegion(split.First, targetKey, first) ??
            FindToolRegion(split.Second, targetKey, second);
    }

    private static LierneDockLocation? DocumentRegionOfGroup(string key) => key switch
    {
        "group:main" => LierneDockLocation.Main,
        _ when key.StartsWith("group:left", StringComparison.Ordinal) => LierneDockLocation.Left,
        _ when key.StartsWith("group:right", StringComparison.Ordinal) => LierneDockLocation.Right,
        _ => null
    };

    private static LierneToolRegion? ToolRegionOfGroup(string key) => key switch
    {
        _ when key.StartsWith("group:left", StringComparison.Ordinal) => LierneToolRegion.Left,
        _ when key.StartsWith("group:right", StringComparison.Ordinal) => LierneToolRegion.Right,
        _ when key.StartsWith("group:bottom", StringComparison.Ordinal) => LierneToolRegion.Bottom,
        _ => null
    };

    private static (LierneDockLocation? First, LierneDockLocation? Second) DocumentRegions(
        string splitKey,
        LierneDockLocation? inherited) => splitKey switch
    {
        "split:left" => (LierneDockLocation.Left, inherited ?? LierneDockLocation.Main),
        "split:right" => (inherited ?? LierneDockLocation.Main, LierneDockLocation.Right),
        "split:bottom" => (inherited ?? LierneDockLocation.Main, null),
        _ when splitKey.StartsWith("split:region.left", StringComparison.Ordinal) =>
            (LierneDockLocation.Left, LierneDockLocation.Left),
        _ when splitKey.StartsWith("split:region.right", StringComparison.Ordinal) =>
            (LierneDockLocation.Right, LierneDockLocation.Right),
        _ => (inherited ?? LierneDockLocation.Main, inherited ?? LierneDockLocation.Main)
    };

    private static (LierneToolRegion? First, LierneToolRegion? Second) ToolRegions(
        string splitKey,
        LierneToolRegion? inherited) => splitKey switch
    {
        "split:left" => (LierneToolRegion.Left, inherited),
        "split:right" => (inherited, LierneToolRegion.Right),
        "split:bottom" => (inherited, LierneToolRegion.Bottom),
        _ when splitKey.StartsWith("split:region.left", StringComparison.Ordinal) =>
            (LierneToolRegion.Left, LierneToolRegion.Left),
        _ when splitKey.StartsWith("split:region.right", StringComparison.Ordinal) =>
            (LierneToolRegion.Right, LierneToolRegion.Right),
        _ when splitKey.StartsWith("split:region.bottom", StringComparison.Ordinal) =>
            (LierneToolRegion.Bottom, LierneToolRegion.Bottom),
        _ => (inherited, inherited)
    };

    private const string DocumentPrefix = "doc:";

    internal static string DocumentKey(string id) => DocumentPrefix + id;
    internal static string PanelKey(ToolWindowId id) => $"panel:{id.Value}";

    private const int PrefixLength = 4;
    private const int PanelPrefixLength = 6;

    internal static LierneDockEntry? FindDocumentEntry(Dictionary<string, LierneDockEntry> entries, string id)
    {
        // Normal document ids contain a sequence number. Keep the stack buffer bounded
        // for restored ids, which can also contain leading zeroes.
        if (id.Length > 64)
        {
            return entries.GetValueOrDefault(DocumentKey(id));
        }

        Span<char> key = stackalloc char[PrefixLength + id.Length];
        DocumentPrefix.AsSpan().CopyTo(key);
        id.AsSpan().CopyTo(key[PrefixLength..]);
        return entries.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(key, out var entry) ? entry : null;
    }

    private static DockNode? BuildRegion(
        LierneToolRegion region,
        IReadOnlyDictionary<LierneDockLocation, List<string>> documents,
        IReadOnlyDictionary<LierneToolRegion, List<string>> panels,
        IReadOnlyDictionary<LierneToolRegion, LiernePanelState> panelStates,
        string? selectedDocument)
    {
        var location = region switch
        {
            LierneToolRegion.Left => LierneDockLocation.Left,
            LierneToolRegion.Right => LierneDockLocation.Right,
            _ => LierneDockLocation.Main
        };
        var prefix = region switch
        {
            LierneToolRegion.Left => "left",
            LierneToolRegion.Right => "right",
            _ => "bottom"
        };
        var documentGroup = BuildGroup($"group:{prefix}.docs",
            documents.GetValueOrDefault(location), selectedDocument, PrefixLength);
        var panelGroup = BuildGroup($"group:{prefix}.panels",
            panels.GetValueOrDefault(region), panelStates.GetValueOrDefault(region)?.ToolKey, PanelPrefixLength);
        return Combine($"split:region.{prefix}", DockAxis.Horizontal, documentGroup, panelGroup);
    }

    private static DockNode? BuildBottom(DockNode? panels, bool hasBottom)
    {
        var host = hasBottom
            ? new DockGroup("group:bottom.host", [BottomKey], BottomKey)
            : null;
        return Combine("split:region.bottom", DockAxis.Horizontal, panels, host);
    }

    private static DockNode? Combine(string key, DockAxis axis, DockNode? first, DockNode? second) =>
        first is null || second is null ? first ?? second : new DockSplit(key, axis, first, second);

    private static DockGroup? BuildGroup(string key, List<string>? items, string? active, int prefixLength)
    {
        if (items is not { Count: > 0 })
        {
            return null;
        }

        var selected = items[0];
        if (active is not null)
        {
            foreach (var item in items)
            {
                if (item.AsSpan(prefixLength).SequenceEqual(active.AsSpan()))
                {
                    selected = item;
                    break;
                }
            }
        }
        return new DockGroup(key, [.. items], selected);
    }

    private static double Size(IReadOnlyDictionary<LierneToolRegion, LiernePanelState> panels, LierneToolRegion region) =>
        panels.GetValueOrDefault(region) is { Size: > 0 } panel
            ? panel.Size
            : region == LierneToolRegion.Bottom ? 150 : 280;

    private static double Fraction(double value) =>
        Math.Clamp(double.IsFinite(value) ? value : 0.5, 0.05, 0.95);

    // Carries the fractions of the already accepted split tree into the rebuilt one, keyed by split
    // key, so a rebuild only invents a fraction for a split that did not exist before. A saved
    // fraction wins over the current tree, because the snapshot is what the user left behind.
    private static DockNode PreserveFractions(
        DockNode? current,
        IReadOnlyDictionary<string, double>? fractions,
        DockNode built)
    {
        if (built is not DockSplit split)
        {
            return built;
        }

        var existing = FindSplit(current, split.Key);
        var saved = fractions is not null && fractions.TryGetValue(split.Key, out var fraction) &&
            double.IsFinite(fraction)
                ? Fraction(fraction)
                : (double?)null;
        var first = PreserveFractions(current, fractions, split.First);
        var second = PreserveFractions(current, fractions, split.Second);
        var resolved = saved ?? existing?.Fraction ?? split.Fraction;
        return ReferenceEquals(split.First, first) && ReferenceEquals(split.Second, second) &&
               split.Fraction == resolved
            ? split
            : split with { First = first, Second = second, Fraction = resolved };
    }

    private static DockSplit? FindSplit(DockNode? node, string key) => node switch
    {
        DockSplit split when split.Key == key => split,
        DockSplit split => FindSplit(split.First, key) ?? FindSplit(split.Second, key),
        _ => null
    };

    // A null result means the item is absent. An unchanged reference means it is already active.
    internal static DockNode? SelectItem(DockNode? node, string key)
    {
        if (node is DockGroup group)
        {
            if (group.ActiveKey == key)
            {
                return group;
            }

            return group.Items.AsSpan().Contains(key) ? group with { ActiveKey = key } : null;
        }

        if (node is not DockSplit split)
        {
            return null;
        }

        if (SelectItem(split.First, key) is { } first)
        {
            return ReferenceEquals(split.First, first) ? split : split with { First = first };
        }

        if (SelectItem(split.Second, key) is { } second)
        {
            return ReferenceEquals(split.Second, second) ? split : split with { Second = second };
        }

        return null;
    }

    internal static bool SameLayout(DockNode? current, DockNode next) =>
        ReferenceEquals(current, next) || (current, next) switch
    {
        (DockGroup first, DockGroup second) => first.Key == second.Key &&
            first.ActiveKey == second.ActiveKey && first.Items.AsSpan().SequenceEqual(second.Items.AsSpan()),
        (DockSplit first, DockSplit second) => first.Key == second.Key &&
            first.Axis == second.Axis && first.Fraction == second.Fraction &&
            SameLayout(first.First, second.First) && SameLayout(first.Second, second.Second),
        _ => false
    };
}
