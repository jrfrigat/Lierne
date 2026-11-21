using System.Globalization;

namespace Lierne;

/// <summary>Chooses how prominently the shell presents a notification.</summary>
public enum LierneNotificationSeverity
{
    /// <summary>Routine information.</summary>
    Info,

    /// <summary>A recoverable problem the user should notice.</summary>
    Warning,

    /// <summary>A failed operation.</summary>
    Error
}

/// <summary>Describes one message shown by the notification host.</summary>
/// <param name="Id">The stable notification id.</param>
/// <param name="Severity">How the host presents the message.</param>
/// <param name="Message">The localized message, when the notification has no error code.</param>
/// <param name="ErrorCode">The stable error code, when the message comes from a failed operation.</param>
/// <param name="ErrorMessage">The technical error detail paired with <paramref name="ErrorCode"/>.</param>
/// <param name="Dismissible">Whether the user can close the notification.</param>
public sealed record LierneNotification(
    string Id,
    LierneNotificationSeverity Severity,
    StringKey? Message = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    bool Dismissible = true)
{
    /// <summary>Creates a localized informational notification.</summary>
    /// <param name="message">The message key.</param>
    /// <returns>The notification.</returns>
    public static LierneNotification Info(StringKey message) =>
        Create(LierneNotificationSeverity.Info, message);

    /// <summary>Creates a localized warning notification.</summary>
    /// <param name="message">The message key.</param>
    /// <returns>The notification.</returns>
    public static LierneNotification Warning(StringKey message) =>
        Create(LierneNotificationSeverity.Warning, message);

    /// <summary>Creates an error notification from a command or service failure.</summary>
    /// <param name="errorCode">The stable error code.</param>
    /// <param name="errorMessage">The technical error detail.</param>
    /// <returns>The notification.</returns>
    public static LierneNotification Error(string? errorCode, string? errorMessage) =>
        new(NextId(), LierneNotificationSeverity.Error,
            ErrorCode: errorCode ?? "lattice.notification.failed",
            ErrorMessage: errorMessage);

    private static LierneNotification Create(LierneNotificationSeverity severity, StringKey message) =>
        new(NextId(), severity, message);

    private static string NextId() =>
        $"notification-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}";
}

/// <summary>Owns the notifications shown by the shell of one host scope.</summary>
public sealed class LierneNotificationService
{
    private const int Capacity = 20;
    private readonly Lock _gate = new();
    private readonly List<LierneNotification> _items = [];

    /// <summary>Raised after a notification is added or dismissed.</summary>
    public event Action? Changed;

    /// <summary>Gets the visible notifications, oldest first.</summary>
    public IReadOnlyList<LierneNotification> Items
    {
        get
        {
            lock (_gate)
            {
                return _items.ToArray();
            }
        }
    }

    /// <summary>Adds a notification and raises <see cref="Changed"/>.</summary>
    /// <param name="notification">The notification to show.</param>
    public void Notify(LierneNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        lock (_gate)
        {
            _items.Add(notification);
            if (_items.Count > Capacity)
            {
                _items.RemoveRange(0, _items.Count - Capacity);
            }
        }

        NotifyChanged();
    }

    /// <summary>Adds an error notification for a failed operation.</summary>
    /// <param name="errorCode">The stable error code.</param>
    /// <param name="errorMessage">The technical error detail.</param>
    public void NotifyError(string? errorCode, string? errorMessage) =>
        Notify(LierneNotification.Error(errorCode, errorMessage));

    /// <summary>Removes one notification.</summary>
    /// <param name="id">The notification id.</param>
    /// <returns>Whether the notification existed.</returns>
    public bool Dismiss(string id)
    {
        bool removed;
        lock (_gate)
        {
            removed = _items.RemoveAll(item => string.Equals(item.Id, id, StringComparison.Ordinal)) > 0;
        }

        if (removed)
        {
            NotifyChanged();
        }

        return removed;
    }

    /// <summary>Removes every notification.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            if (_items.Count == 0)
            {
                return;
            }

            _items.Clear();
        }

        NotifyChanged();
    }

    private void NotifyChanged()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError("A Lierne notification observer failed: {0}", exception);
        }
    }
}
