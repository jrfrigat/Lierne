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

    /// <summary>The reserved namespace of literal text that needs no resource catalog.</summary>
    public const string LiteralNamespace = "lattice.text";

    /// <summary>Creates a key that resolves to the supplied text in every culture.</summary>
    /// <param name="text">The text, for example the title of an untitled document.</param>
    /// <returns>The literal key.</returns>
    /// <exception cref="ArgumentException">The text is null, empty or whitespace.</exception>
    public static StringKey Literal(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return new StringKey(LiteralNamespace, text);
    }

    /// <summary>Gets the namespace that owns the string.</summary>
    public string Namespace { get; }

    /// <summary>Gets the name of the string inside the namespace.</summary>
    public string Name { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Namespace}.{Name}";
}
