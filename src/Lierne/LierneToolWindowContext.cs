namespace Lierne;

/// <summary>Carries the tool window and the scoped services to a plugin tool-window component.</summary>
public sealed class LierneToolWindowContext
{
    /// <summary>Creates the tool-window context.</summary>
    /// <param name="toolWindow">The tool window instance being rendered.</param>
    /// <param name="commands">The command service of the current host scope.</param>
    /// <param name="toolWindows">The tool-window service that owns the instance.</param>
    /// <param name="localizer">The localizer of the current host scope.</param>
    /// <param name="services">The scoped service provider of the running host.</param>
    public LierneToolWindowContext(
        LierneToolWindowInstance toolWindow,
        ILierneCommandService commands,
        ILierneToolWindowService toolWindows,
        ILierneLocalizer localizer,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(toolWindow);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(toolWindows);
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(services);
        ToolWindow = toolWindow;
        Commands = commands;
        ToolWindows = toolWindows;
        Localizer = localizer;
        Services = services;
    }

    /// <summary>Gets the tool window instance being rendered.</summary>
    public LierneToolWindowInstance ToolWindow { get; }

    /// <summary>Gets the tool window id.</summary>
    public ToolWindowId Id => ToolWindow.Id;

    /// <summary>Gets the command service of the current host scope.</summary>
    public ILierneCommandService Commands { get; }

    /// <summary>Gets the tool-window service that owns the instance.</summary>
    public ILierneToolWindowService ToolWindows { get; }

    /// <summary>Gets the localizer of the current host scope.</summary>
    public ILierneLocalizer Localizer { get; }

    /// <summary>Gets the scoped service provider of the running host.</summary>
    public IServiceProvider Services { get; }

    /// <summary>Shows this tool window.</summary>
    /// <returns>Whether the tool window exists.</returns>
    public LierneResult Show() => ToolWindows.Show(Id);

    /// <summary>Hides this tool window.</summary>
    /// <returns>Whether the tool window exists.</returns>
    public LierneResult Hide() => ToolWindows.Hide(Id);
}
