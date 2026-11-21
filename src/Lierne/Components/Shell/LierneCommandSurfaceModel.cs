namespace Lierne;

/// <summary>One menu group after command placements and legacy items are merged.</summary>
internal sealed record LierneMenuGroupView(
    MenuGroupDescriptor Group,
    IReadOnlyList<CommandPlacementDescriptor> Commands,
    IReadOnlyList<MenuItemDescriptor> LegacyItems);

/// <summary>One ribbon group after command placements and legacy items are merged.</summary>
internal sealed record LierneRibbonGroupView(
    RibbonGroupDescriptor Group,
    IReadOnlyList<CommandPlacementDescriptor> Items);
