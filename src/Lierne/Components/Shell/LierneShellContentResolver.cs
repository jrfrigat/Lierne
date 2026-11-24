namespace Lierne;

/// <summary>Resolves the winning plugin contribution for a shell surface.</summary>
internal static class LierneShellContentResolver
{
    internal static ShellContentDescriptor? Resolve(LiernePluginCatalog catalog, LierneShellSlot slot) =>
        catalog.ShellContentFor(slot);
}
