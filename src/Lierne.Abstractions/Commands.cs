namespace Lierne;

/// <summary>Describes the outcome of a command execution.</summary>
/// <param name="Success">Whether the command completed.</param>
/// <param name="Data">The command result data, when any.</param>
/// <param name="ErrorCode">The stable error code when the command failed.</param>
/// <param name="ErrorMessage">The human-readable error message when the command failed.</param>
public sealed record LierneCommandResult(
    bool Success,
    object? Data = null,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    /// <summary>Creates a successful result.</summary>
    /// <param name="data">The result data, when any.</param>
    /// <returns>The successful result.</returns>
    public static LierneCommandResult Ok(object? data = null) => new(true, data);

    /// <summary>Creates a failed result.</summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="message">The human-readable error message.</param>
    /// <returns>The failed result.</returns>
    public static LierneCommandResult Fail(string code, string message) =>
        new(false, null, code, message);
}

/// <summary>Carries the services, the current state and the argument to a command handler.</summary>
public sealed class LierneCommandContext
{
    /// <summary>Creates the command context.</summary>
    /// <param name="services">The scoped service provider of the running host.</param>
    /// <param name="state">The shared Lierne state.</param>
    /// <param name="argument">The typed argument, or <see langword="null"/>.</param>
    public LierneCommandContext(
        IServiceProvider services,
        ILierneContext state,
        object? argument)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(state);
        Services = services;
        State = state;
        Argument = argument;
    }

    /// <summary>Gets the scoped service provider of the running host.</summary>
    public IServiceProvider Services { get; }

    /// <summary>Gets the shared Lierne state.</summary>
    public ILierneContext State { get; }

    /// <summary>Gets the typed argument, or <see langword="null"/>.</summary>
    public object? Argument { get; }
}

/// <summary>Executes one command.</summary>
/// <param name="context">The command context.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns>The command result.</returns>
public delegate Task<LierneCommandResult> LierneCommandHandler(
    LierneCommandContext context,
    CancellationToken cancellationToken);

/// <summary>Describes the behavior of a contributed command.</summary>
/// <param name="Descriptor">The command metadata.</param>
/// <param name="Handler">The handler that executes the command.</param>
/// <param name="CanExecute">The optional predicate over the shared state.</param>
/// <param name="Plugin">The plugin that contributed the command, stamped by the builder.</param>
public sealed record LierneCommandRegistration(
    CommandDescriptor Descriptor,
    LierneCommandHandler Handler,
    Func<ILierneContext, bool>? CanExecute = null,
    PluginId? Plugin = null);

/// <summary>Describes one command execution passed to the observers.</summary>
/// <param name="Command">The executed command.</param>
/// <param name="Argument">The typed argument, or <see langword="null"/>.</param>
/// <param name="Context">The command context.</param>
public sealed record LierneCommandExecution(
    CommandId Command,
    object? Argument,
    LierneCommandContext Context);

/// <summary>Observes command executions in registration order.</summary>
/// <remarks>An observer failure is reported through tracing and does not affect the command or other observers.</remarks>
public interface ILierneCommandObserver
{
    /// <summary>Called before the command handler runs.</summary>
    /// <param name="execution">The execution.</param>
    /// <returns>A task that completes when the observer is done.</returns>
    ValueTask OnExecutingAsync(LierneCommandExecution execution);

    /// <summary>Called after the command handler runs, including when it fails.</summary>
    /// <param name="execution">The execution.</param>
    /// <param name="result">The command result.</param>
    /// <returns>A task that completes when the observer is done.</returns>
    ValueTask OnExecutedAsync(LierneCommandExecution execution, LierneCommandResult result);
}

/// <summary>Executes the commands contributed by the plugins.</summary>
public interface ILierneCommandService
{
    /// <summary>Gets the command registrations in plugin order.</summary>
    IReadOnlyList<LierneCommandRegistration> Registrations { get; }

    /// <summary>Checks whether a command exists and its predicate allows execution.</summary>
    /// <param name="command">The command id.</param>
    /// <returns>Whether the command can run.</returns>
    bool CanExecute(CommandId command);

    /// <summary>Executes a command.</summary>
    /// <param name="command">The command id.</param>
    /// <param name="argument">The typed argument, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The command result.</returns>
    Task<LierneCommandResult> ExecuteAsync(
        CommandId command,
        object? argument = null,
        CancellationToken cancellationToken = default);
}
