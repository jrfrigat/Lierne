namespace Lattice;

/// <summary>Describes one localized string catalog contributed by a plugin.</summary>
/// <param name="Culture">The two-letter culture, for example <c>en</c> or <c>ru</c>.</param>
/// <param name="Strings">The strings by full <see cref="StringKey"/> text.</param>
public sealed record LatticeStringCatalog(
    string Culture,
    IReadOnlyDictionary<string, string> Strings);
