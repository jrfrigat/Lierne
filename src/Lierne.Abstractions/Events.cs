namespace Lierne;

/// <summary>Publishes typed in-process events inside one Lierne host scope.</summary>
public interface ILierneEventBus
{
    /// <summary>Subscribes to events of one type.</summary>
    /// <typeparam name="T">The event type.</typeparam>
    /// <param name="handler">The handler called for every published event.</param>
    /// <returns>A subscription that stops delivery when disposed.</returns>
    IDisposable Subscribe<T>(Action<T> handler);

    /// <summary>Publishes an event to the current subscribers.</summary>
    /// <typeparam name="T">The event type.</typeparam>
    /// <param name="message">The event payload.</param>
    void Publish<T>(T message);
}
