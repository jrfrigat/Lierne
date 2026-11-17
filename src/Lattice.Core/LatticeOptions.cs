namespace Lattice;

/// <summary>Collects the plugins an application is composed from.</summary>
public sealed class LatticeOptions
{
    private readonly List<ILatticePlugin> _plugins = [];

    /// <summary>Gets the plugins in declaration order.</summary>
    public IReadOnlyList<ILatticePlugin> Plugins => _plugins;

    /// <summary>Adds a plugin by type.</summary>
    /// <typeparam name="TPlugin">The plugin type, which must have a public parameterless constructor.</typeparam>
    /// <returns>The same options instance.</returns>
    public LatticeOptions AddPlugin<TPlugin>()
        where TPlugin : ILatticePlugin, new()
    {
        _plugins.Add(new TPlugin());
        return this;
    }

    /// <summary>Adds a plugin instance.</summary>
    /// <param name="plugin">The plugin.</param>
    /// <returns>The same options instance.</returns>
    public LatticeOptions AddPlugin(ILatticePlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        _plugins.Add(plugin);
        return this;
    }
}
