using System.Collections.Immutable;
using System.Globalization;
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
internal sealed record LierneDockEntry(
    LierneDockEntryKind Kind,
    LierneDocumentInstance? Document = null,
    PanelPlacement? Panel = null);

/// <summary>The dock topology and catalog built from the semantic Lierne workspace.</summary>
internal sealed record LierneDockModel(
    IReadOnlyList<DockItem> Items,
    DockNode Layout,
    IReadOnlyDictionary<string, LierneDockEntry> Entries,
    IReadOnlyDictionary<LierneToolRegion, string?> ActivePanels,
    IReadOnlyDictionary<LierneDockLocation, string?> SelectedDocuments,
    string LayoutSignature,
    string ItemSignature);

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
        IReadOnlyDictionary<LierneToolRegion, string?> activePanels,
        IReadOnlyDictionary<LierneDockLocation, string?> selectedDocuments,
        IReadOnlyDictionary<LierneToolRegion, double> regionSizes,
        Func<StringKey, string> localize,
        string startTitle,
        string bottomTitle,
        bool hasStart,
        bool hasBottom)
    {
        var visible = tools
            .Where(tool => tool.Visible)
            .ToDictionary(tool => tool.Id);
        var placementById = placements.ToDictionary(placement => placement.Panel);
        var panelOrder = placements
            .Where(placement => visible.ContainsKey(placement.Panel))
            .OrderBy(placement => placement.Region)
            .ThenBy(placement => placement.Slot)
            .ThenBy(placement => placement.Order)
            .ThenBy(placement => placement.Panel.Value, StringComparer.Ordinal)
            .ToArray();
        var descriptorById = descriptors.ToDictionary(descriptor => descriptor.Id);
        var entries = new Dictionary<string, LierneDockEntry>(StringComparer.Ordinal);
        var items = new List<DockItem>();
        var panelKeys = new Dictionary<LierneToolRegion, List<string>>();
        foreach (var placement in panelOrder)
        {
            var key = PanelKey(placement.Panel);
            entries[key] = new LierneDockEntry(LierneDockEntryKind.Panel, Panel: placement);
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
            entries[key] = new LierneDockEntry(LierneDockEntryKind.Document, Document: document);
            items.Add(new DockItem(key, localize(document.Title), KeepAlive: true, CanClose: true));
            if (!documentKeys.TryGetValue(document.Location, out var list))
            {
                documentKeys[document.Location] = list = [];
            }

            list.Add(key);
        }

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

        var resolvedPanels = new Dictionary<LierneToolRegion, string?>();
        foreach (var region in Enum.GetValues<LierneToolRegion>())
        {
            var keys = panelKeys.GetValueOrDefault(region) ?? [];
            var active = activePanels.GetValueOrDefault(region);
            var activeKey = active is { } tool && keys.Contains(PanelKey(new ToolWindowId(tool)))
                ? PanelKey(new ToolWindowId(tool))
                : keys.FirstOrDefault();
            resolvedPanels[region] = activeKey is null ? null : activeKey[PrefixLength..];
        }

        var resolvedDocuments = new Dictionary<LierneDockLocation, string?>();
        foreach (var location in Enum.GetValues<LierneDockLocation>())
        {
            var keys = documentKeys.GetValueOrDefault(location) ?? [];
            var candidate = activeDocument?.Location == location
                ? DocumentKey(activeDocument.Id)
                : selectedDocuments.GetValueOrDefault(location) is { } selected
                    ? DocumentKey(selected)
                    : null;
            var activeKey = candidate is not null && keys.Contains(candidate) ? candidate : keys.FirstOrDefault();
            resolvedDocuments[location] = activeKey is null ? null : activeKey[PrefixLength..];
        }

        var main = (DockNode?)BuildGroup("group:main",
            documentKeys.GetValueOrDefault(LierneDockLocation.Main) ??
            (hasStart ? [StartKey] : []),
            resolvedDocuments.GetValueOrDefault(LierneDockLocation.Main) is { } mainId
                ? DocumentKey(mainId)
                : hasStart ? StartKey : null) ?? new DockGroup("group:main", []);
        var left = BuildRegion(LierneToolRegion.Left, documentKeys, panelKeys, resolvedPanels,
            resolvedDocuments);
        var right = BuildRegion(LierneToolRegion.Right, documentKeys, panelKeys, resolvedPanels,
            resolvedDocuments);
        var bottom = BuildBottom(BuildGroup("group:bottom.panels",
            panelKeys.GetValueOrDefault(LierneToolRegion.Bottom) ?? [],
            resolvedPanels.GetValueOrDefault(LierneToolRegion.Bottom) is { } bottomPanel
                ? PanelKey(new ToolWindowId(bottomPanel))
                : null), hasBottom);

        var layout = main;
        var mainWidth = 800d;
        var leftWidth = Size(regionSizes, LierneToolRegion.Left);
        var rightWidth = Size(regionSizes, LierneToolRegion.Right);
        if (right is not null)
        {
            layout = new DockSplit("split:right", DockAxis.Horizontal, layout, right,
                Fraction(rightWidth / (mainWidth + rightWidth)));
        }

        if (left is not null)
        {
            layout = new DockSplit("split:left", DockAxis.Horizontal, left, layout,
                Fraction(leftWidth / (leftWidth + mainWidth + rightWidth)));
        }

        if (bottom is not null)
        {
            var bottomHeight = Size(regionSizes, LierneToolRegion.Bottom);
            layout = new DockSplit("split:bottom", DockAxis.Vertical, layout, bottom,
                Fraction(bottomHeight / (bottomHeight + 600)));
        }

        return new LierneDockModel(
            items,
            layout,
            entries,
            resolvedPanels,
            resolvedDocuments,
            LayoutSignature(main, left, right, bottom, layout, panelKeys, documentKeys, hasStart, hasBottom,
                resolvedPanels, resolvedDocuments),
            ItemSignature(items));
    }

    internal static bool TryDocument(string key, out string documentId)
    {
        if (key.StartsWith("doc:", StringComparison.Ordinal))
        {
            documentId = key[PrefixLength..];
            return documentId.Length > 0;
        }

        documentId = string.Empty;
        return false;
    }

    internal static bool TryPanel(string key, out ToolWindowId panel)
    {
        if (key.StartsWith("panel:", StringComparison.Ordinal))
        {
            var value = key[PrefixLength..];
            if (value.Length > 0)
            {
                panel = new ToolWindowId(value);
                return true;
            }
        }

        panel = default;
        return false;
    }

    internal static LierneDockLocation? DocumentRegionOf(DockNode root, string targetKey) =>
        FindDocumentRegion(root, targetKey, null);

    internal static LierneToolRegion? ToolRegionOf(DockNode root, string targetKey) =>
        FindToolRegion(root, targetKey, null);

    internal static bool TargetsBottom(string targetKey) =>
        targetKey.StartsWith("group:bottom", StringComparison.Ordinal) ||
        targetKey.StartsWith("split:region.bottom", StringComparison.Ordinal);

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

    internal static string DocumentKey(string id) => $"doc:{id}";
    internal static string PanelKey(ToolWindowId id) => $"panel:{id.Value}";

    private const int PrefixLength = 4;

    private static DockNode? BuildRegion(
        LierneToolRegion region,
        IReadOnlyDictionary<LierneDockLocation, List<string>> documents,
        IReadOnlyDictionary<LierneToolRegion, List<string>> panels,
        IReadOnlyDictionary<LierneToolRegion, string?> activePanels,
        IReadOnlyDictionary<LierneDockLocation, string?> selectedDocuments)
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
            documents.GetValueOrDefault(location) ?? [],
            selectedDocuments.GetValueOrDefault(location) is { } document
                ? DocumentKey(document)
                : null);
        var panelGroup = BuildGroup($"group:{prefix}.panels",
            panels.GetValueOrDefault(region) ?? [],
            activePanels.GetValueOrDefault(region) is { } panel
                ? PanelKey(new ToolWindowId(panel))
                : null);
        return Combine($"split:region.{prefix}", DockAxis.Horizontal, documentGroup, panelGroup);
    }

    private static DockNode? BuildBottom(DockNode? panels, bool hasBottom)
    {
        var host = hasBottom
            ? BuildGroup("group:bottom.host", [BottomKey], BottomKey)
            : null;
        return Combine("split:region.bottom", DockAxis.Horizontal, panels, host);
    }

    private static DockNode? Combine(string key, DockAxis axis, params DockNode?[] nodes)
    {
        var present = nodes.Where(node => node is not null).Cast<DockNode>().ToArray();
        return present.Length switch
        {
            0 => null,
            1 => present[0],
            2 => new DockSplit(key, axis, present[0], present[1], 0.5),
            _ => new DockSplit(key, axis, present[0],
                Combine($"{key}.rest", axis, present[1..]) ??
                new DockGroup($"{key}.empty", []), 0.5)
        };
    }

    private static DockGroup? BuildGroup(string key, IReadOnlyList<string> items, string? active)
    {
        if (items.Count == 0)
        {
            return null;
        }

        var selected = active is not null && items.Contains(active) ? active : items[0];
        return new DockGroup(key, [.. items], selected);
    }

    private static double Size(IReadOnlyDictionary<LierneToolRegion, double> sizes, LierneToolRegion region) =>
        sizes.TryGetValue(region, out var size) && size > 0 ? size : region == LierneToolRegion.Bottom ? 150 : 280;

    private static double Fraction(double value) =>
        Math.Clamp(double.IsFinite(value) ? value : 0.5, 0.05, 0.95);

    private static string LayoutSignature(
        DockNode main,
        DockNode? left,
        DockNode? right,
        DockNode? bottom,
        DockNode root,
        IReadOnlyDictionary<LierneToolRegion, List<string>> panels,
        IReadOnlyDictionary<LierneDockLocation, List<string>> documents,
        bool hasStart,
        bool hasBottom,
        IReadOnlyDictionary<LierneToolRegion, string?> activePanels,
        IReadOnlyDictionary<LierneDockLocation, string?> selectedDocuments)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append(NodeSignature(root)).Append('|');
        foreach (var region in Enum.GetValues<LierneToolRegion>())
        {
            builder.Append(region).Append('=')
                .Append(string.Join(',', panels.GetValueOrDefault(region) ?? []))
                .Append(':').Append(activePanels.GetValueOrDefault(region)).Append('|');
        }

        foreach (var location in Enum.GetValues<LierneDockLocation>())
        {
            builder.Append(location).Append('=')
                .Append(string.Join(',', documents.GetValueOrDefault(location) ?? []))
                .Append(':').Append(selectedDocuments.GetValueOrDefault(location)).Append('|');
        }

        return builder.Append(hasStart).Append(hasBottom).ToString();
    }

    private static string NodeSignature(DockNode? node) => node switch
    {
        null => "-",
        DockGroup group => $"g({group.Key}:{group.ActiveKey}:{string.Join(',', group.Items)})",
        DockSplit split => $"s({split.Key}:{split.Axis.ToString()}:{split.Fraction.ToString("R", CultureInfo.InvariantCulture)}:" +
            $"{NodeSignature(split.First)},{NodeSignature(split.Second)})",
        _ => "?"
    };

    private static string ItemSignature(IReadOnlyList<DockItem> items) =>
        string.Join('|', items.Select(item =>
            $"{item.Key}:{item.Title}:{item.KeepAlive}:{item.CanClose}"));
}
