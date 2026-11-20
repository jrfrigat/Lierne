using System.Resources;

namespace Lierne.Core.Tests;

/// <summary>Test strings of the <c>app</c> namespace used by the runtime tests.</summary>
internal static class TestStrings
{
    /// <summary>Gets the resource manager of the test strings.</summary>
    public static ResourceManager ResourceManager { get; } =
        new("Lierne.Core.Tests.Resources.TestStrings", typeof(TestStrings).Assembly);
}
