namespace Lattice;

/// <summary>Identifies a localized string contributed by a plugin.</summary>
public readonly record struct StringKey
{
    /// <summary>Creates a string key.</summary>
    /// <param name="namespace">The namespace that owns the string, usually the plugin id.</param>
    /// <param name="name">The name of the string inside the namespace.</param>
    /// <exception cref="ArgumentException">Either part is null, empty or whitespace.</exception>
    public StringKey(string @namespace, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(@namespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Namespace = @namespace;
        Name = name;
    }

    /// <summary>Gets the namespace that owns the string.</summary>
    public string Namespace { get; }

    /// <summary>Gets the name of the string inside the namespace.</summary>
    public string Name { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Namespace}.{Name}";
}
