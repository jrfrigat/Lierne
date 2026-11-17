namespace Lattice;

/// <summary>
/// Observable state shared by the plugins, commands and the host. Plugins define the keys they
/// publish; Lattice does not know what a connection or a document means.
/// </summary>
public interface ILatticeContext
{
    /// <summary>Raised after a value or the key set changes.</summary>
    event Action? Changed;

    /// <summary>Gets the current values.</summary>
    IReadOnlyDictionary<string, object?> Values { get; }

    /// <summary>Reads a typed value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The plugin-defined key.</param>
    /// <param name="value">The value when it exists and has the requested type.</param>
    /// <returns>Whether the value exists and has the requested type.</returns>
    bool TryGet<T>(string key, out T? value);

    /// <summary>Reads a typed value or the type default.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The plugin-defined key.</param>
    /// <returns>The value, or the type default.</returns>
    T? Get<T>(string key);

    /// <summary>Stores a value and raises <see cref="Changed"/> when it differs.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The plugin-defined key.</param>
    /// <param name="value">The value.</param>
    void Set<T>(string key, T value);

    /// <summary>Removes a value and raises <see cref="Changed"/> when it existed.</summary>
    /// <param name="key">The plugin-defined key.</param>
    void Remove(string key);
}
