using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Lattice;

/// <summary>Executes commands, applies their predicates and notifies the observers.</summary>
public sealed class LatticeCommandService : ILatticeCommandService
{
    private readonly Dictionary<CommandId, LatticeCommandRegistration> _registrations;
    private readonly ILatticeContext _context;
    private readonly IServiceProvider _services;

    /// <summary>Creates the command service.</summary>
    /// <param name="catalog">The composed plugin catalog.</param>
    /// <param name="context">The shared state used by the command predicates.</param>
    /// <param name="services">The scoped service provider of the running host.</param>
    public LatticeCommandService(
        LatticePluginCatalog catalog,
        ILatticeContext context,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(services);
        Registrations = catalog.CommandRegistrations;
        _registrations = catalog.CommandRegistrations
            .ToDictionary(registration => registration.Descriptor.Id);
        _context = context;
        _services = services;
    }

    /// <inheritdoc />
    public IReadOnlyList<LatticeCommandRegistration> Registrations { get; }

    /// <inheritdoc />
    public bool CanExecute(CommandId command) =>
        _registrations.TryGetValue(command, out var registration) &&
        (registration.CanExecute?.Invoke(_context) ?? true);

    /// <inheritdoc />
    public async Task<LatticeCommandResult> ExecuteAsync(
        CommandId command,
        object? argument = null,
        CancellationToken cancellationToken = default)
    {
        if (!_registrations.TryGetValue(command, out var registration))
        {
            return Localize(LatticeCommandResult.Fail(
                "lattice.command.not_found",
                $"The command '{command}' is not registered."));
        }

        if (!(registration.CanExecute?.Invoke(_context) ?? true))
        {
            return Localize(LatticeCommandResult.Fail(
                "lattice.command.disabled",
                $"The command '{command}' is disabled."));
        }

        if (!ArgumentMatches(registration.Descriptor.ArgumentType, argument))
        {
            return Localize(LatticeCommandResult.Fail(
                "lattice.command.invalid_argument",
                $"The command '{command}' does not accept the supplied argument."));
        }

        var context = new LatticeCommandContext(_services, _context, argument);
        var execution = new LatticeCommandExecution(command, argument, context);
        var observers = _services.GetServices<ILatticeCommandObserver>().ToArray();
        foreach (var observer in observers)
        {
            try
            {
                await observer.OnExecutingAsync(execution);
            }
            catch (Exception exception)
            {
                Trace.TraceError("Observer {0} failed before command {1}: {2}", observer.GetType(), command, exception);
            }
        }

        LatticeCommandResult result;
        try
        {
            result = await registration.Handler(context, cancellationToken)
                ?? LatticeCommandResult.Fail(
                    "lattice.command.null_result",
                    $"The command '{command}' returned no result.");
        }
        catch (OperationCanceledException)
        {
            result = LatticeCommandResult.Fail(
                "lattice.command.cancelled",
                $"The command '{command}' was cancelled.");
        }
        catch (Exception exception)
        {
            result = LatticeCommandResult.Fail("lattice.command.failed", exception.Message);
        }

        result = Localize(result);
        foreach (var observer in observers)
        {
            try
            {
                await observer.OnExecutedAsync(execution, result);
            }
            catch (Exception exception)
            {
                Trace.TraceError("Observer {0} failed after command {1}: {2}", observer.GetType(), command, exception);
            }
        }

        return result;
    }

    private LatticeCommandResult Localize(LatticeCommandResult result)
    {
        if (result.Success || result.ErrorCode is not { } code)
        {
            return result;
        }

        var localizer = _services.GetService<ILatticeLocalizer>();
        if (localizer is null)
        {
            return result;
        }

        var message = localizer.GetError(code, result.ErrorMessage);
        return string.Equals(message, result.ErrorMessage, StringComparison.Ordinal)
            ? result
            : result with { ErrorMessage = message };
    }

    private static bool ArgumentMatches(Type? argumentType, object? argument) =>
        argumentType is null
            ? argument is null
            : argument is not null && argumentType.IsInstanceOfType(argument);
}
