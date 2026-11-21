using System.Diagnostics;

namespace Lierne;

/// <summary>Publishes typed in-process events inside one Lierne host scope.</summary>
public sealed class LierneEventBus : ILierneEventBus
{
    private readonly Lock _lock = new();
    private readonly Dictionary<Type, List<Delegate>> _handlers = [];

    /// <inheritdoc />
    public IDisposable Subscribe<T>(Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(T), out var handlers))
            {
                handlers = [];
                _handlers.Add(typeof(T), handlers);
            }

            handlers.Add(handler);
        }

        return new Subscription(this, typeof(T), handler);
    }

    /// <inheritdoc />
    public void Publish<T>(T message)
    {
        Delegate[] handlers;
        lock (_lock)
        {
            handlers = _handlers.TryGetValue(typeof(T), out var list) ? [.. list] : [];
        }

        foreach (var handler in handlers)
        {
            try
            {
                ((Action<T>)handler)(message);
            }
            catch (Exception exception)
            {
                Trace.TraceError("A Lierne event handler failed: {0}", exception);
            }
        }
    }

    private void Unsubscribe(Type type, Delegate handler)
    {
        lock (_lock)
        {
            if (!_handlers.TryGetValue(type, out var handlers))
            {
                return;
            }

            handlers.Remove(handler);
            if (handlers.Count == 0)
            {
                _handlers.Remove(type);
            }
        }
    }

    private sealed class Subscription(LierneEventBus owner, Type type, Delegate handler) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Unsubscribe(type, handler);
            }
        }
    }
}
