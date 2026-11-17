namespace Lattice;

/// <summary>Describes the outcome of a command execution.</summary>
/// <param name="Success">Whether the command completed.</param>
/// <param name="Data">The command result data, when any.</param>
/// <param name="ErrorCode">The stable error code when the command failed.</param>
/// <param name="ErrorMessage">The human-readable error message when the command failed.</param>
public sealed record LatticeCommandResult(
    bool Success,
    object? Data = null,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    /// <summary>Creates a successful result.</summary>
    /// <param name="data">The result data, when any.</param>
    /// <returns>The successful result.</returns>
    public static LatticeCommandResult Ok(object? data = null) => new(true, data);

    /// <summary>Creates a failed result.</summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="message">The human-readable error message.</param>
    /// <returns>The failed result.</returns>
    public static LatticeCommandResult Fail(string code, string message) =>
        new(false, null, code, message);
}

/// <summary>Carries the services, the current state and the argument to a command handler.</summary>
public sealed class LatticeCommandContext
{
    /// <summary>Creates the command context.</summary>
    /// <param name="services">The scoped service provider of the running host.</param>
    /// <param name="state">The shared Lattice state.</param>
    /// <param name="argument">The typed argument, or <see langword="null"/>.</param>
    public LatticeCommandContext(
        IServiceProvider services,
        ILatticeContext state,
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

    /// <summary>Gets the shared Lattice state.</summary>
    public ILatticeContext State { get; }

    /// <summary>Gets the typed argument, or <see langword="null"/>.</summary>
    public object? Argument { get; }
}

/// <summary>Executes one command.</summary>
/// <param name="context">The command context.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns>The command result.</returns>
public delegate Task<LatticeCommandResult> LatticeCommandHandler(
    LatticeCommandContext context,
    CancellationToken cancellationToken);

/// <summary>Describes the behavior of a contributed command.</summary>
/// <param name="Descriptor">The command metadata.</param>
/// <param name="Handler">The handler that executes the command.</param>
/// <param name="CanExecute">The optional predicate over the shared state.</param>
public sealed record LatticeCommandRegistration(
    CommandDescriptor Descriptor,
    LatticeCommandHandler Handler,
    Func<ILatticeContext, bool>? CanExecute = null);

/// <summary>Describes one command execution passed to the observers.</summary>
/// <param name="Command">The executed command.</param>
/// <param name="Argument">The typed argument, or <see langword="null"/>.</param>
/// <param name="Context">The command context.</param>
public sealed record LatticeCommandExecution(
    CommandId Command,
    object? Argument,
    LatticeCommandContext Context);

/// <summary>Observes command executions in registration order.</summary>
public interface ILatticeCommandObserver
{
    /// <summary>Called before the command handler runs.</summary>
    /// <param name="execution">The execution.</param>
    /// <returns>A task that completes when the observer is done.</returns>
    ValueTask OnExecutingAsync(LatticeCommandExecution execution);

    /// <summary>Called after the command handler runs, including when it fails.</summary>
    /// <param name="execution">The execution.</param>
    /// <param name="result">The command result.</param>
    /// <returns>A task that completes when the observer is done.</returns>
    ValueTask OnExecutedAsync(LatticeCommandExecution execution, LatticeCommandResult result);
}

/// <summary>Executes the commands contributed by the plugins.</summary>
public interface ILatticeCommandService
{
    /// <summary>Gets the command registrations in plugin order.</summary>
    IReadOnlyList<LatticeCommandRegistration> Registrations { get; }

    /// <summary>Checks whether a command exists and its predicate allows execution.</summary>
    /// <param name="command">The command id.</param>
    /// <returns>Whether the command can run.</returns>
    bool CanExecute(CommandId command);

    /// <summary>Executes a command.</summary>
    /// <param name="command">The command id.</param>
    /// <param name="argument">The typed argument, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The command result.</returns>
    Task<LatticeCommandResult> ExecuteAsync(
        CommandId command,
        object? argument = null,
        CancellationToken cancellationToken = default);
}
