using Flare.Icons;

namespace Lattice;

/// <summary>Maps a descriptor icon key to a Flare icon in the host application.</summary>
public interface ILatticeIconResolver
{
    /// <summary>Resolves an icon key.</summary>
    /// <param name="iconKey">The icon key declared by the descriptor, or <see langword="null"/>.</param>
    /// <returns>The Flare icon, or <see langword="null"/> when the key is unset or unknown.</returns>
    FlareIcon? Resolve(string? iconKey);
}

/// <summary>The default resolver: descriptors without an icon.</summary>
public sealed class NullLatticeIconResolver : ILatticeIconResolver
{
    /// <inheritdoc />
    public FlareIcon? Resolve(string? iconKey) => null;
}
