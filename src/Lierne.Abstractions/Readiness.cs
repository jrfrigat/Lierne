namespace Lierne;

/// <summary>Identifies one readiness gate that commands, views or background work can wait for.</summary>
/// <param name="Value">The stable scope value chosen by the host or a plugin.</param>
public readonly record struct LierneReadinessScope(string Value)
{
    /// <summary>Gets the scope of the composed plugin catalog.</summary>
    public static LierneReadinessScope Catalog { get; } = new("catalog");

    /// <summary>Gets the scope of the open workspace.</summary>
    public static LierneReadinessScope Workspace { get; } = new("workspace");

    /// <summary>Gets the scope of one document or editor.</summary>
    public static LierneReadinessScope Document { get; } = new("document");

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Describes a readiness transition.</summary>
/// <param name="Scope">The scope that changed.</param>
/// <param name="IsReady">Whether the scope is ready.</param>
/// <param name="Reason">Why the scope is not ready, when it is pending.</param>
public sealed record LierneReadinessChange(
    LierneReadinessScope Scope,
    bool IsReady,
    StringKey? Reason);

/// <summary>Coordinates readiness gates for commands, views and background work.</summary>
public interface ILierneReadinessService
{
    /// <summary>Raised after a scope becomes pending or ready.</summary>
    event Action<LierneReadinessChange>? Changed;

    /// <summary>Checks whether a scope is ready.</summary>
    /// <param name="scope">The scope to check.</param>
    /// <returns>Whether no pending work is registered for the scope.</returns>
    bool IsReady(LierneReadinessScope scope);

    /// <summary>Completes when the scope becomes ready.</summary>
    /// <param name="scope">The scope to wait for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the scope is ready.</returns>
    Task WhenReadyAsync(LierneReadinessScope scope, CancellationToken cancellationToken = default);

    /// <summary>Marks the scope as pending until the returned token is disposed.</summary>
    /// <param name="scope">The scope to make pending.</param>
    /// <param name="reason">The localized reason shown while the scope is pending.</param>
    /// <returns>A token that releases the pending state when disposed.</returns>
    IDisposable BeginPending(LierneReadinessScope scope, StringKey reason);
}
