using System.Diagnostics;

namespace Lierne;

/// <summary>A typed, code-first extension point with runtime subscriptions.</summary>
/// <typeparam name="T">The extension contract.</typeparam>
public sealed class LierneExtensionPoint<T> : IExtensionPoint<T>
{
    private readonly Lock _lock = new();
    private readonly List<T> _extensions = [];

    /// <summary>Creates an extension point.</summary>
    /// <param name="id">The stable extension point id.</param>
    public LierneExtensionPoint(ExtensionPointId id)
    {
        if (string.IsNullOrWhiteSpace(id.Value))
        {
            throw new ArgumentException("An extension point id needs a non-empty value.", nameof(id));
        }

        Id = id;
    }

    /// <inheritdoc />
    public ExtensionPointId Id { get; }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public IReadOnlyList<T> Extensions
    {
        get
        {
            lock (_lock)
            {
                return [.. _extensions];
            }
        }
    }

    /// <inheritdoc />
    public IDisposable Add(T extension)
    {
        if (extension is null)
        {
            throw new ArgumentNullException(nameof(extension));
        }

        lock (_lock)
        {
            _extensions.Add(extension);
        }

        Notify();
        return new Subscription(this, extension);
    }

    private void Remove(T extension)
    {
        lock (_lock)
        {
            if (!_extensions.Remove(extension))
            {
                return;
            }
        }

        Notify();
    }

    private void Notify()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception exception)
        {
            Trace.TraceError("A Lierne extension point observer failed: {0}", exception);
        }
    }

    private sealed class Subscription(LierneExtensionPoint<T> owner, T extension) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Remove(extension);
            }
        }
    }
}
