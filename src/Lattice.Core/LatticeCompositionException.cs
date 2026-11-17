namespace Lattice;

/// <summary>Thrown when a plugin composition contains errors.</summary>
public sealed class LatticeCompositionException : Exception
{
    /// <summary>Creates the exception from the composition diagnostics.</summary>
    /// <param name="diagnostics">The diagnostics that block the composition.</param>
    public LatticeCompositionException(IReadOnlyList<LatticeDiagnostic> diagnostics)
        : base(BuildMessage(diagnostics))
    {
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the diagnostics that block the composition.</summary>
    public IReadOnlyList<LatticeDiagnostic> Diagnostics { get; }

    private static string BuildMessage(IReadOnlyList<LatticeDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        return string.Join(
            Environment.NewLine,
            new[] { "The Lattice plugin composition is invalid:" }
                .Concat(diagnostics.Select(diagnostic =>
                    $"[{diagnostic.Severity}] {diagnostic.Code}: {diagnostic.Message}")));
    }
}
