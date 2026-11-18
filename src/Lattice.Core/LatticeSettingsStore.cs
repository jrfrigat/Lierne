namespace Lattice;

/// <summary>Schema-driven settings store over the settings declared by the plugins.</summary>
public sealed class LatticeSettingsStore : ISettingsStore
{
    private readonly Dictionary<SettingsKey, SettingDescriptor> _descriptors;
    private readonly Dictionary<SettingsKey, object?> _values = [];

    /// <summary>Creates the store and seeds every declared default.</summary>
    /// <param name="catalog">The composed plugin catalog.</param>
    public LatticeSettingsStore(LatticePluginCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _descriptors = catalog.Settings.ToDictionary(descriptor => descriptor.Key);
        foreach (var descriptor in catalog.Settings)
        {
            _values[descriptor.Key] = descriptor.DefaultValue;
        }
    }

    /// <inheritdoc />
    public event Action<SettingsKey>? Changed;

    /// <inheritdoc />
    public object? GetValue(SettingsKey key) =>
        _values.TryGetValue(key, out var value) ? value : null;

    /// <inheritdoc />
    public T? Get<T>(SettingsKey key) => GetValue(key) is T typed ? typed : default;

    /// <inheritdoc />
    public LatticeResult Set(SettingsKey key, object? value)
    {
        if (!_descriptors.TryGetValue(key, out var descriptor))
        {
            return LatticeResult.Fail(
                "lattice.settings.unknown_key",
                $"The setting '{key}' is not registered.");
        }

        if (!IsValid(descriptor, value, out var error))
        {
            return LatticeResult.Fail("lattice.settings.invalid_value", error);
        }

        _values[key] = value;
        Changed?.Invoke(key);
        return LatticeResult.Ok;
    }

    internal static bool IsValid(SettingDescriptor descriptor, object? value, out string error)
    {
        error = string.Empty;
        if (value is null)
        {
            return true;
        }

        switch (descriptor.Kind)
        {
            case LatticeSettingKind.Text when value is string:
            case LatticeSettingKind.Boolean when value is bool:
                return true;

            case LatticeSettingKind.Integer when value is int number:
                if (descriptor.Min is { } min && number < min)
                {
                    error = $"The setting '{descriptor.Key}' must be at least {min}.";
                    return false;
                }

                if (descriptor.Max is { } max && number > max)
                {
                    error = $"The setting '{descriptor.Key}' must be at most {max}.";
                    return false;
                }

                return true;

            case LatticeSettingKind.Select when value is string selected:
                if (descriptor.Choices is { } choices &&
                    !choices.Any(choice => string.Equals(
                        choice.Value,
                        selected,
                        StringComparison.Ordinal)))
                {
                    error = $"The setting '{descriptor.Key}' does not allow the value '{selected}'.";
                    return false;
                }

                return true;

            default:
                error = $"The setting '{descriptor.Key}' expects a {descriptor.Kind} value.";
                return false;
        }
    }
}
