namespace Lierne.Components.Tests;

/// <summary>Guards that the host package does not pick or depend on a Flare theme.</summary>
public sealed class ThemeBoundaryTests
{
    [Fact]
    public void TheHostAssemblyDoesNotReferenceAFlareTheme()
    {
        foreach (var reference in typeof(LierneShell).Assembly.GetReferencedAssemblies())
        {
            Assert.False(
                reference.Name?.StartsWith("Flare.Theme.", StringComparison.Ordinal) == true,
                $"Lierne must not reference {reference.Name}; the application registers the theme.");
        }
    }
}
