using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Lierne;

/// <summary>Executes commands, applies their predicates and notifies the observers.</summary>
public sealed class LierneCommandService : ILierneCommandService, ICommandPresentationService
{
    private readonly Dictionary<CommandId, LierneCommandRegistration> _registrations;
    private readonly LiernePluginCatalog _catalog;
    private readonly IReadOnlyList<CommandDescriptor> _allCommands;
    private readonly ILierneContext _context;
    private readonly IServiceProvider _services;
    private readonly Lock _presentationLock = new();
    private readonly Dictionary<CommandId, CommandPresentation> _presentations = [];
    private readonly Dictionary<CommandId, long> _pendingUpdates = [];
    private long _updateSequence;

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
        _catalog = catalog;
        _allCommands = Array.AsReadOnly(catalog.Commands.ToArray());
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
    public event Action<CommandId>? Changed;

    /// <inheritdoc />
    public CommandPresentation GetPresentation(CommandId command)
    {
        lock (_presentationLock)
        {
            if (_presentations.TryGetValue(command, out var cached))
            {
                return cached;
            }
        }

        var presentation = CreateDefaultPresentation(command);
        if (!_registrations.ContainsKey(command))
        {
            return presentation;
        }

        lock (_presentationLock)
        {
            return _presentations.TryAdd(command, presentation) ? presentation : _presentations[command];
        }
    }

    /// <inheritdoc />
    public async ValueTask<CommandPresentation> UpdateAsync(
        CommandId command,
        object? argument = null,
        CancellationToken cancellationToken = default)
    {
        if (!_registrations.ContainsKey(command))
        {
            return GetPresentation(command);
        }

        CommandPresentation presentation;
        long request = 0;
        if (_catalog.CommandUpdateHandlers.TryGetValue(command, out var updateHandler))
        {
            lock (_presentationLock)
            {
                _pendingUpdates[command] = request = ++_updateSequence;
            }

            var context = new LierneCommandContext(_services, _context, argument);
            try
            {
                presentation = await updateHandler.UpdateAsync(context, cancellationToken);
                if (presentation.Command != command)
                {
                    Trace.TraceError(
                        "The update handler for command {0} returned the presentation of {1}.",
                        command,
                        presentation.Command);
                    presentation = CreateDefaultPresentation(command) with { Enabled = false };
                }
            }
            catch (OperationCanceledException)
            {
                lock (_presentationLock)
                {
                    EndUpdate(command, request);
                }
                throw;
            }
            catch (Exception exception)
            {
                Trace.TraceError("The update handler for command {0} failed: {1}", command, exception);
                presentation = CreateDefaultPresentation(command) with { Enabled = false };
            }
        }
        else
        {
            CommandPresentation? cached;
            lock (_presentationLock)
            {
                _presentations.TryGetValue(command, out cached);
            }

            presentation = CreateDefaultPresentation(command, cached);
        }

        lock (_presentationLock)
        {
            if ((request != 0 && !EndUpdate(command, request)) ||
                (_presentations.TryGetValue(command, out var cached) && cached == presentation))
            {
                return presentation;
            }

            _presentations[command] = presentation;
        }

        Notify(command);
        return presentation;
    }

    private bool EndUpdate(CommandId command, long request) =>
        _pendingUpdates.TryGetValue(command, out var pending) && pending == request &&
        _pendingUpdates.Remove(command);

    /// <inheritdoc />
    public IReadOnlyList<CommandDescriptor> Search(string query)
    {
        var text = query.AsSpan().Trim();
        if (text.IsEmpty)
        {
            return _allCommands;
        }

        List<CommandDescriptor>? matches = null;
        for (var index = 0; index < _allCommands.Count; index++)
        {
            var command = _allCommands[index];
            if (Matches(command, text))
            {
                (matches ??= []).Add(command);
            }
        }

        return matches is null ? [] : matches.ToArray();
    }

    /// <inheritdoc />
    public void Invalidate(CommandId? command = null)
    {
        if (command is { } id)
        {
            lock (_presentationLock)
            {
                _presentations.Remove(id);
                _pendingUpdates.Remove(id);
            }

            Notify(id);
            return;
        }

        CommandId[] commands;
        lock (_presentationLock)
        {
            commands = _presentations.Keys.ToArray();
            _presentations.Clear();
            _pendingUpdates.Clear();
        }

        foreach (var changedCommand in commands)
        {
            Notify(changedCommand);
        }
    }

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

    private CommandPresentation CreateDefaultPresentation(CommandId command, CommandPresentation? cached = null)
    {
        if (!_registrations.TryGetValue(command, out var registration))
        {
            return new CommandPresentation(
                command,
                new StringKey("lierne", command.Value),
                Enabled: false,
                Visible: false);
        }

        var descriptor = registration.Descriptor;
        var bindings = _services.GetService<ILierneShortcutService>()?.Bindings;
        string? shortcut = null;
        if (bindings is not null)
        {
            for (var index = 0; index < bindings.Count; index++)
            {
                if (bindings[index].Command == command)
                {
                    shortcut = bindings[index].EffectiveShortcut;
                    break;
                }
            }
        }
        var enabled = registration.CanExecute?.Invoke(_context) ?? true;
        if (cached is not null && cached.Enabled == enabled && cached.ShortcutText == shortcut)
        {
            return cached;
        }

        return new CommandPresentation(
            command,
            descriptor.Title,
            descriptor.Description,
            descriptor.IconKey,
            enabled,
            Visible: true,
            Checked: false,
            ShortcutText: shortcut);
    }

    private void Notify(CommandId command)
    {
        try
        {
            Changed?.Invoke(command);
        }
        catch (Exception exception)
        {
            Trace.TraceError("A command presentation observer failed: {0}", exception);
        }
    }

    private static bool Matches(CommandDescriptor command, ReadOnlySpan<char> query)
    {
        if (Contains(command.Id.Value, query) ||
            Contains(command.Title.Namespace, query) ||
            Contains(command.Title.Name, query) ||
            (command.Description is { } description &&
                (Contains(description.Namespace, query) || Contains(description.Name, query))))
        {
            return true;
        }

        if (command.Keywords is { } keywords)
        {
            for (var index = 0; index < keywords.Count; index++)
            {
                if (Contains(keywords[index], query))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool Contains(string? value, ReadOnlySpan<char> query) =>
        value.AsSpan().Contains(query, StringComparison.OrdinalIgnoreCase);

    private LierneCommandResult Localize(LierneCommandResult result)    {
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
