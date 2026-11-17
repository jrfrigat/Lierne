using System.Resources;

namespace Lattice;

/// <summary>The embedded error messages of the Lattice runtime.</summary>
/// <remarks>
/// The neutral <c>Resources/LatticeCoreStrings.resx</c> holds the English text and
/// <c>LatticeCoreStrings.ru.resx</c> the Russian translation. The keys are the stable error
/// codes returned by the runtime services.
/// </remarks>
internal static class LatticeCoreStrings
{
    /// <summary>Gets the resource manager of the runtime error messages.</summary>
    public static ResourceManager ResourceManager { get; } =
        new("Lattice.Resources.LatticeCoreStrings", typeof(LatticeCoreStrings).Assembly);
}
