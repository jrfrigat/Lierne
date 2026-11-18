using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lattice;

/// <summary>Identifies the independently stored parts of a Lattice session.</summary>
public enum LatticeStateSection
{
    /// <summary>Settings and keyboard shortcuts.</summary>
    Preferences,

    /// <summary>Documents, tools, and the shell layout.</summary>
    Workspace
}

/// <summary>Reads and atomically replaces a host's versioned Lattice state documents.</summary>
public interface ILatticeStateStorage
{
    /// <summary>Reads a section, or returns null when it has never been stored.</summary>
    ValueTask<string?> ReadAsync(LatticeStateSection section, CancellationToken cancellationToken = default);

    /// <summary>Replaces the stored JSON for one section.</summary>
    ValueTask WriteAsync(LatticeStateSection section, string json, CancellationToken cancellationToken = default);
}

/// <summary>Lets a plugin serialize and migrate the payload of its persistent documents.</summary>
/// <param name="Version">The positive schema version written by the current plugin.</param>
/// <param name="Write">Produces the JSON for one payload.</param>
/// <param name="Read">Restores a payload using the version stored with it.</param>
public sealed record LatticeDocumentStateCodec(
    int Version,
    Func<object?, JsonElement> Write,
    Func<int, JsonElement, object?> Read);

/// <summary>Converts a stored state section from a host's legacy schema to the next version.</summary>
/// <param name="Section">The section this migration owns.</param>
/// <param name="FromVersion">The version to upgrade.</param>
/// <param name="Upgrade">Updates the JSON fields. Lattice advances the envelope version.</param>
public sealed record LatticeStateMigration(LatticeStateSection Section, int FromVersion, Action<JsonObject> Upgrade);
