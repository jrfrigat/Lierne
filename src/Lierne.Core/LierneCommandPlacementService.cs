namespace Lierne;

/// <summary>Owns the command placements contributed by the plugins.</summary>
public sealed class LierneCommandPlacementService : ICommandPlacementService
{
    private readonly IReadOnlyList<CommandPlacementDescriptor> _placements;
    private readonly Dictionary<CommandSurface, IReadOnlyList<CommandPlacementDescriptor>> _bySurface;

    /// <summary>Creates the placement service from the composed catalog.</summary>
    /// <param name="catalog">The composed plugin catalog.</param>
    public LierneCommandPlacementService(LiernePluginCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _placements = catalog.CommandPlacements;
        _bySurface = _placements
            .GroupBy(placement => placement.Surface)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CommandPlacementDescriptor>)Array.AsReadOnly(group
                    .OrderBy(placement => placement.Order)
                    .ThenBy(placement => placement.Command.Value, StringComparer.Ordinal)
                    .ToArray()));
    }

    /// <inheritdoc />
    public IReadOnlyList<CommandPlacementDescriptor> All => _placements;

    /// <inheritdoc />
    public IReadOnlyList<CommandPlacementDescriptor> ForSurface(CommandSurface surface) =>
        _bySurface.TryGetValue(surface, out var placements) ? placements : [];
}
