using System.Resources;

namespace Lierne;

/// <summary>The embedded error messages of the Lierne runtime.</summary>
/// <remarks>
/// The neutral <c>Resources/LierneCoreStrings.resx</c> holds the English text and
/// <c>LierneCoreStrings.ru.resx</c> the Russian translation. The keys are the stable error
/// codes returned by the runtime services.
/// </remarks>
internal static class LierneCoreStrings
{
    /// <summary>Gets the resource manager of the runtime error messages.</summary>
    public static ResourceManager ResourceManager { get; } =
        new("Lierne.Resources.LierneCoreStrings", typeof(LierneCoreStrings).Assembly);
}
