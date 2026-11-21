namespace Lierne;

/// <summary>Describes how a progress operation is presented.</summary>
public enum LierneProgressKind
{
    /// <summary>Progress that does not block the rest of the workspace.</summary>
    Background,

    /// <summary>Progress that blocks the caller until it completes or is canceled.</summary>
    Modal
}

/// <summary>Describes the lifecycle stage of one progress operation.</summary>
public enum LierneProgressState
{
    /// <summary>The operation has started.</summary>
    Started,

    /// <summary>The operation reported progress.</summary>
    Reported,

    /// <summary>The operation completed.</summary>
    Completed,

    /// <summary>The operation was canceled.</summary>
    Canceled
}

/// <summary>Describes one progress update.</summary>
/// <param name="Title">The localized operation title.</param>
/// <param name="Kind">Whether the operation is background or modal.</param>
/// <param name="State">The lifecycle stage of the operation.</param>
/// <param name="Fraction">The completed fraction from 0 to 1, or <see langword="null"/> for indeterminate progress.</param>
/// <param name="Detail">The localized detail line, when any.</param>
public sealed record LierneProgressUpdate(
    StringKey Title,
    LierneProgressKind Kind,
    LierneProgressState State,
    double? Fraction = null,
    StringKey? Detail = null);

/// <summary>Reports and cancels one progress operation.</summary>
public interface ILierneProgressHandle : IAsyncDisposable
{
    /// <summary>Gets the token that is canceled when the operation is canceled.</summary>
    CancellationToken CancellationToken { get; }

    /// <summary>Reports progress.</summary>
    /// <param name="fraction">The completed fraction from 0 to 1, or <see langword="null"/> for indeterminate progress.</param>
    /// <param name="detail">The localized detail line, when any.</param>
    void Report(double? fraction, StringKey? detail = null);

    /// <summary>Cancels the operation.</summary>
    void Cancel();
}

/// <summary>Starts progress operations for one host scope.</summary>
public interface ILierneProgressService
{
    /// <summary>Raised after a progress operation changes state.</summary>
    event Action<LierneProgressUpdate>? Changed;

    /// <summary>Starts a progress operation.</summary>
    /// <param name="title">The localized operation title.</param>
    /// <param name="kind">Whether the operation is background or modal.</param>
    /// <returns>The handle that reports and cancels the operation.</returns>
    ILierneProgressHandle Start(
        StringKey title,
        LierneProgressKind kind = LierneProgressKind.Background);
}
