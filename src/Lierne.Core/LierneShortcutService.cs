namespace Lierne;

/// <summary>Owns the declared command shortcuts and the user overrides.</summary>
public sealed class LierneShortcutService : ILierneShortcutService
{
    private readonly List<LierneShortcutBinding> _bindings;

    /// <summary>Creates the shortcut service from the composed catalog.</summary>
    /// <param name="catalog">The composed plugin catalog.</param>
    public LierneShortcutService(LiernePluginCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _bindings = catalog.CommandRegistrations
            .Select(registration =>
            {
                var shortcut = Normalize(registration.Descriptor.DefaultShortcut);
                return new LierneShortcutBinding(
                    registration.Descriptor.Id,
                    registration.Descriptor.Title,
                    shortcut,
                    shortcut);
            })
            .ToList();
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public IReadOnlyList<LierneShortcutBinding> Bindings => _bindings;

    /// <inheritdoc />
    public LierneResult ApplyOverrides(IReadOnlyDictionary<string, string>? overrides)
    {
        var updated = _bindings.ToList();
        if (overrides is not null)
        {
            foreach (var pair in overrides)
            {
                var index = updated.FindIndex(binding =>
                    string.Equals(binding.Command.Value, pair.Key, StringComparison.Ordinal));
                if (index < 0)
                {
                    return LierneResult.Fail(
                        "lattice.shortcut.unknown_command",
                        $"The command '{pair.Key}' is not registered.");
                }

                updated[index] = updated[index] with
                {
                    EffectiveShortcut = Normalize(pair.Value)
                };
            }
        }

        _bindings.Clear();
        _bindings.AddRange(updated);
        Changed?.Invoke();
        return LierneResult.Ok;
    }

    /// <inheritdoc />
    public LierneResult<CommandId> Resolve(string gesture)
    {
        var normalized = gesture.AsSpan().Trim();
        if (normalized.IsEmpty)
        {
            return LierneResult<CommandId>.Fail(
                "lattice.shortcut.invalid_gesture",
                "A shortcut gesture is required.");
        }

        foreach (var binding in _bindings)
        {
            if (normalized.Equals(binding.EffectiveShortcut.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return LierneResult<CommandId>.Ok(binding.Command);
            }
        }

        return LierneResult<CommandId>.Fail(
            "lattice.shortcut.not_found",
            $"No command uses the shortcut '{normalized}'.");
    }

    /// <inheritdoc />
    public IReadOnlyList<CommandId> ResolveAll(string gesture)
    {
        var normalized = gesture.AsSpan().Trim();
        if (normalized.IsEmpty)
        {
            return [];
        }

        var commands = new List<CommandId>();
        foreach (var binding in _bindings)
        {
            if (normalized.Equals(binding.EffectiveShortcut.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                commands.Add(binding.Command);
            }
        }

        return commands;
    }

    /// <inheritdoc />
    public IReadOnlyList<LierneShortcutConflict> FindConflicts()
    {
        return _bindings
            .Where(binding => !string.IsNullOrEmpty(binding.EffectiveShortcut))
            .GroupBy(
                binding => binding.EffectiveShortcut!,
                StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => new LierneShortcutConflict(
                group.Key,
                group.Select(binding => binding.Command).ToArray()))
            .ToArray();
    }

    private static string? Normalize(string? gesture) =>
        string.IsNullOrWhiteSpace(gesture) ? null : gesture.Trim();
}
