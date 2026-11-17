using System.Resources;

namespace Lattice;

/// <summary>Describes one localized string catalog contributed by a plugin.</summary>
/// <param name="Namespace">The <see cref="StringKey.Namespace"/> the resources serve.</param>
/// <param name="Resources">
/// The resource manager whose neutral resources hold the English strings and whose satellite
/// assemblies hold the other cultures.
/// </param>
public sealed record LatticeStringCatalog(
    string Namespace,
    ResourceManager Resources);
