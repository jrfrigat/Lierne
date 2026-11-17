namespace Lattice;

/// <summary>Owns the declared command shortcuts and the user overrides.</summary>
public sealed class LatticeShortcutService : ILatticeShortcutService
{
    private readonly List<LatticeShortcutBinding> _bindings;

    /// <summary>Creates the shortcut service from the composed catalog.</summary>
    /// <param name="catalog">The composed plugin catalog.</param>
    public LatticeShortcutService(LatticePluginCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _bindings = catalog.CommandRegistrations
            .Select(registration =>
            {
                var shortcut = Normalize(registration.Descriptor.DefaultShortcut);
                return new LatticeShortcutBinding(
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
    public IReadOnlyList<LatticeShortcutBinding> Bindings => _bindings;

    /// <inheritdoc />
    public LatticeResult ApplyOverrides(IReadOnlyDictionary<string, string>? overrides)
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
                    return LatticeResult.Fail(
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
        return LatticeResult.Ok;
    }

    /// <inheritdoc />
    public LatticeResult<CommandId> Resolve(string gesture)
    {
        var normalized = Normalize(gesture);
        if (normalized is null)
        {
            return LatticeResult<CommandId>.Fail(
                "lattice.shortcut.invalid_gesture",
                "A shortcut gesture is required.");
        }

        var binding = _bindings.FirstOrDefault(candidate =>
            string.Equals(candidate.EffectiveShortcut, normalized, StringComparison.OrdinalIgnoreCase));
        return binding is null
            ? LatticeResult<CommandId>.Fail(
                "lattice.shortcut.not_found",
                $"No command uses the shortcut '{normalized}'.")
            : LatticeResult<CommandId>.Ok(binding.Command);
    }

    /// <inheritdoc />
    public IReadOnlyList<LatticeShortcutConflict> FindConflicts()
    {
        return _bindings
            .Where(binding => !string.IsNullOrEmpty(binding.EffectiveShortcut))
            .GroupBy(
                binding => binding.EffectiveShortcut!,
                StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => new LatticeShortcutConflict(
                group.Key,
                group.Select(binding => binding.Command).ToArray()))
            .ToArray();
    }

    private static string? Normalize(string? gesture) =>
        string.IsNullOrWhiteSpace(gesture) ? null : gesture.Trim();
}
