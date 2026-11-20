namespace Lierne;

/// <summary>Observable, plugin-keyed state shared by the host and the commands.</summary>
public sealed class LierneContext : ILierneContext
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?> Values => _values;

    /// <inheritdoc />
    public bool TryGet<T>(string key, out T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (_values.TryGetValue(key, out var candidate) && candidate is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    public T? Get<T>(string key) => TryGet<T>(key, out var value) ? value : default;

    /// <inheritdoc />
    public void Set<T>(string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (_values.TryGetValue(key, out var current) && Equals(current, value))
        {
            return;
        }

        _values[key] = value;
        Changed?.Invoke();
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (_values.Remove(key))
        {
            Changed?.Invoke();
        }
    }
}
