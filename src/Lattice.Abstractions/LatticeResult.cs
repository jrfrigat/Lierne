namespace Lattice;

/// <summary>Describes the outcome of a Lattice operation that returns no data.</summary>
/// <param name="Success">Whether the operation completed.</param>
/// <param name="ErrorCode">The stable error code when the operation failed.</param>
/// <param name="ErrorMessage">The human-readable error message when the operation failed.</param>
public readonly record struct LatticeResult(
    bool Success,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    /// <summary>Gets a successful result.</summary>
    public static LatticeResult Ok { get; } = new(true);

    /// <summary>Creates a failed result.</summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="message">The human-readable error message.</param>
    /// <returns>The failed result.</returns>
    public static LatticeResult Fail(string code, string message) => new(false, code, message);
}

/// <summary>Describes the outcome of a Lattice operation that returns data.</summary>
/// <typeparam name="T">The data type.</typeparam>
/// <param name="Success">Whether the operation completed.</param>
/// <param name="Data">The data when the operation succeeded.</param>
/// <param name="ErrorCode">The stable error code when the operation failed.</param>
/// <param name="ErrorMessage">The human-readable error message when the operation failed.</param>
public readonly record struct LatticeResult<T>(
    bool Success,
    T? Data,
    string? ErrorCode,
    string? ErrorMessage)
{
    /// <summary>Creates a successful result.</summary>
    /// <param name="data">The data.</param>
    /// <returns>The successful result.</returns>
    public static LatticeResult<T> Ok(T data) => new(true, data, null, null);

    /// <summary>Creates a failed result.</summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="message">The human-readable error message.</param>
    /// <returns>The failed result.</returns>
    public static LatticeResult<T> Fail(string code, string message) =>
        new(false, default, code, message);
}
