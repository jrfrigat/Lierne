namespace Lierne;

/// <summary>Owns the command placements contributed by the plugins.</summary>
public sealed class LierneCommandPlacementService : ICommandPlacementService
{
    private readonly IReadOnlyList<CommandPlacementDescriptor> _placements;

    /// <summary>Creates the placement service from the composed catalog.</summary>
    /// <param name="catalog">The composed plugin catalog.</param>
    public LierneCommandPlacementService(LiernePluginCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _placements = catalog.CommandPlacements;
    }

    /// <inheritdoc />
    public IReadOnlyList<CommandPlacementDescriptor> All => _placements;

    /// <inheritdoc />
    public IReadOnlyList<CommandPlacementDescriptor> ForSurface(CommandSurface surface) =>
        _placements
            .Where(placement => placement.Surface == surface)
            .OrderBy(placement => placement.Order)
            .ThenBy(placement => placement.Command.Value, StringComparer.Ordinal)
            .ToArray();
}
