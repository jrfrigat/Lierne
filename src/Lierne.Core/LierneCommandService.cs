using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Lierne;

/// <summary>Executes commands, applies their predicates and notifies the observers.</summary>
public sealed class LierneCommandService : ILierneCommandService
{
    private readonly Dictionary<CommandId, LierneCommandRegistration> _registrations;
    private readonly ILierneContext _context;
    private readonly IServiceProvider _services;

    /// <summary>Creates the command service.</summary>
    /// <param name="catalog">The composed plugin catalog.</param>
    /// <param name="context">The shared state used by the command predicates.</param>
    /// <param name="services">The scoped service provider of the running host.</param>
    public LierneCommandService(
        LiernePluginCatalog catalog,
        ILierneContext context,
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
    public IReadOnlyList<LierneCommandRegistration> Registrations { get; }

    /// <inheritdoc />
    public bool CanExecute(CommandId command) =>
        _registrations.TryGetValue(command, out var registration) &&
        (registration.CanExecute?.Invoke(_context) ?? true);

    /// <inheritdoc />
    public async Task<LierneCommandResult> ExecuteAsync(
        CommandId command,
        object? argument = null,
        CancellationToken cancellationToken = default)
    {
        if (!_registrations.TryGetValue(command, out var registration))
        {
            return Localize(LierneCommandResult.Fail(
                "lattice.command.not_found",
                $"The command '{command}' is not registered."));
        }

        if (!(registration.CanExecute?.Invoke(_context) ?? true))
        {
            return Localize(LierneCommandResult.Fail(
                "lattice.command.disabled",
                $"The command '{command}' is disabled."));
        }

        if (!ArgumentMatches(registration.Descriptor.ArgumentType, argument))
        {
            return Localize(LierneCommandResult.Fail(
                "lattice.command.invalid_argument",
                $"The command '{command}' does not accept the supplied argument."));
        }

        var context = new LierneCommandContext(_services, _context, argument);
        var execution = new LierneCommandExecution(command, argument, context);
        var observers = _services.GetServices<ILierneCommandObserver>().ToArray();
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

        LierneCommandResult result;
        try
        {
            result = await registration.Handler(context, cancellationToken)
                ?? LierneCommandResult.Fail(
                    "lattice.command.null_result",
                    $"The command '{command}' returned no result.");
        }
        catch (OperationCanceledException)
        {
            result = LierneCommandResult.Fail(
                "lattice.command.cancelled",
                $"The command '{command}' was cancelled.");
        }
        catch (Exception exception)
        {
            result = LierneCommandResult.Fail("lattice.command.failed", exception.Message);
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

    private LierneCommandResult Localize(LierneCommandResult result)
    {
        if (result.Success || result.ErrorCode is not { } code)
        {
            return result;
        }

        var localizer = _services.GetService<ILierneLocalizer>();
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
