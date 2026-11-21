namespace Lierne;

/// <summary>Identifies one typed extension point contributed by a plugin.</summary>
/// <param name="Value">The stable extension point id.</param>
public readonly record struct ExtensionPointId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>A typed, code-first extension point.</summary>
/// <typeparam name="T">The extension contract.</typeparam>
public interface IExtensionPoint<T>
{
    /// <summary>Gets the stable extension point id.</summary>
    ExtensionPointId Id { get; }

    /// <summary>Gets the current extensions in registration order.</summary>
    IReadOnlyList<T> Extensions { get; }

    /// <summary>Adds one extension until the returned subscription is disposed.</summary>
    /// <param name="extension">The extension instance.</param>
    /// <returns>A subscription that removes the extension when disposed.</returns>
    IDisposable Add(T extension);

    /// <summary>Raised after an extension is added or removed.</summary>
    event Action? Changed;
}
