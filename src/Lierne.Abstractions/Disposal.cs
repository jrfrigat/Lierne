namespace Lierne;

/// <summary>Collects disposable resources and releases them in reverse order.</summary>
public sealed class LierneDisposableBag : IDisposable
{
    private readonly Lock _lock = new();
    private readonly List<IDisposable> _items = [];
    private bool _disposed;

    /// <summary>Adds a resource to the bag.</summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="item">The resource to release when the bag is disposed.</param>
    /// <returns>The same resource.</returns>
    /// <exception cref="ObjectDisposedException">The bag has already been disposed.</exception>
    public T Add<T>(T item) where T : IDisposable
    {
        ArgumentNullException.ThrowIfNull(item);
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _items.Add(item);
        }

        return item;
    }

    /// <summary>Releases every resource in reverse order.</summary>
    /// <exception cref="AggregateException">One or more resources failed to dispose.</exception>
    public void Dispose()
    {
        IDisposable[] items;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            items = [.. _items];
            _items.Clear();
        }

        List<Exception>? errors = null;
        for (var index = items.Length - 1; index >= 0; index--)
        {
            try
            {
                items[index].Dispose();
            }
            catch (Exception exception)
            {
                (errors ??= []).Add(exception);
            }
        }

        if (errors is not null)
        {
            throw new AggregateException("One or more disposable resources failed.", errors);
        }
    }
}
