namespace Lierne;

/// <summary>Thrown when a plugin composition contains errors.</summary>
public sealed class LierneCompositionException : Exception
{
    /// <summary>Creates the exception from the composition diagnostics.</summary>
    /// <param name="diagnostics">The diagnostics that block the composition.</param>
    public LierneCompositionException(IReadOnlyList<LierneDiagnostic> diagnostics)
        : base(BuildMessage(diagnostics))
    {
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the diagnostics that block the composition.</summary>
    public IReadOnlyList<LierneDiagnostic> Diagnostics { get; }

    private static string BuildMessage(IReadOnlyList<LierneDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        return string.Join(
            Environment.NewLine,
            new[] { "The Lierne plugin composition is invalid:" }
                .Concat(diagnostics.Select(diagnostic =>
                    $"[{diagnostic.Severity}] {diagnostic.Code}: {diagnostic.Message}")));
    }
}
