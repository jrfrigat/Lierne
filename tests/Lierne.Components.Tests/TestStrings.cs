using System.Resources;

namespace Lierne.Components.Tests;

/// <summary>Test strings of the <c>app</c> namespace used by the shell render tests.</summary>
internal static class TestStrings
{
    /// <summary>Gets the resource manager of the test strings.</summary>
    public static ResourceManager ResourceManager { get; } =
        new("Lierne.Components.Tests.Resources.TestStrings", typeof(TestStrings).Assembly);
}
