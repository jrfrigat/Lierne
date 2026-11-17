using System.Resources;

namespace Lattice;

/// <summary>The embedded strings of the built-in Lattice host surfaces.</summary>
/// <remarks>
/// The neutral <c>Resources/LatticeHostStrings.resx</c> holds the English text and
/// <c>LatticeHostStrings.ru.resx</c> the Russian translation. The keys are the
/// <see cref="StringKey.Name"/> parts of the <c>lattice</c> namespace.
/// </remarks>
public static class LatticeHostStrings
{
    /// <summary>Gets the resource manager that serves the <c>lattice</c> string namespace.</summary>
    public static ResourceManager ResourceManager { get; } =
        new("Lattice.Resources.LatticeHostStrings", typeof(LatticeHostStrings).Assembly);
}
