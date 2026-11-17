using System.Resources;

namespace Lattice.Core.Tests;

/// <summary>Test strings of the <c>app</c> namespace used by the runtime tests.</summary>
internal static class TestStrings
{
    /// <summary>Gets the resource manager of the test strings.</summary>
    public static ResourceManager ResourceManager { get; } =
        new("Lattice.Core.Tests.Resources.TestStrings", typeof(TestStrings).Assembly);
}
