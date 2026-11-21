using System.Reflection;

namespace Lierne.Core.Tests;

/// <summary>Guards the dependency boundary between the semantic core and the visual layer.</summary>
public sealed class ArchitectureBoundaryTests
{
    [Fact]
    public void AbstractionsDoNotReferenceFlareOrBlazor()
    {
        AssertNoVisualReferences(typeof(LierneDocumentInstance).Assembly);
    }

    [Fact]
    public void CoreDoesNotReferenceFlareOrBlazor()
    {
        AssertNoVisualReferences(typeof(LiernePluginCatalog).Assembly);
    }

    private static void AssertNoVisualReferences(Assembly assembly)
    {
        foreach (var reference in assembly.GetReferencedAssemblies())
        {
            var name = reference.Name ?? string.Empty;
            Assert.False(
                name.StartsWith("Flare", StringComparison.Ordinal),
                $"{assembly.GetName().Name} must not reference {name}.");
            Assert.False(
                name.StartsWith("Microsoft.AspNetCore.Components", StringComparison.Ordinal),
                $"{assembly.GetName().Name} must not reference {name}.");
        }
    }
}
