namespace Lierne;

/// <summary>Carries the scoped services to a plugin toolbar component.</summary>
public sealed class LierneToolbarContext
{
    /// <summary>Creates the toolbar context.</summary>
    /// <param name="commands">The command service of the current host scope.</param>
    /// <param name="localizer">The localizer of the current host scope.</param>
    /// <param name="services">The scoped service provider of the running host.</param>
    public LierneToolbarContext(
        ILierneCommandService commands,
        ILierneLocalizer localizer,
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
    public ILierneCommandService Commands { get; }

    /// <summary>Gets the localizer of the current host scope.</summary>
    public ILierneLocalizer Localizer { get; }

    /// <summary>Gets the scoped service provider of the running host.</summary>
    public IServiceProvider Services { get; }
}
