using System.Resources;

namespace Lierne;

/// <summary>The embedded strings of the built-in Lierne host surfaces.</summary>
/// <remarks>
/// The neutral <c>Resources/LierneHostStrings.resx</c> holds the English text and
/// <c>LierneHostStrings.ru.resx</c> the Russian translation. The keys are the
/// <see cref="StringKey.Name"/> parts of the <c>lattice</c> namespace.
/// </remarks>
public static class LierneHostStrings
{
    /// <summary>Gets the resource manager that serves the <c>lattice</c> string namespace.</summary>
    public static ResourceManager ResourceManager { get; } =
        new("Lierne.Resources.LierneHostStrings", typeof(LierneHostStrings).Assembly);
}
