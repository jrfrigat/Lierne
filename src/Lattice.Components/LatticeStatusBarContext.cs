namespace Lattice;

/// <summary>Carries the scoped services to a plugin status-bar component.</summary>
public sealed class LatticeStatusBarContext
{
    /// <summary>Creates the status-bar context.</summary>
    /// <param name="commands">The command service of the current host scope.</param>
    /// <param name="localizer">The localizer of the current host scope.</param>
    /// <param name="services">The scoped service provider of the running host.</param>
    public LatticeStatusBarContext(
        ILatticeCommandService commands,
        ILatticeLocalizer localizer,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(services);
        Commands = commands;
        Localizer = localizer;
        Services = services;
    }

    /// <summary>Gets the command service of the current host scope.</summary>
    public ILatticeCommandService Commands { get; }

    /// <summary>Gets the localizer of the current host scope.</summary>
    public ILatticeLocalizer Localizer { get; }

    /// <summary>Gets the scoped service provider of the running host.</summary>
    public IServiceProvider Services { get; }
}
