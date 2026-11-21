using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lierne;

/// <summary>Identifies the independently stored parts of a Lierne session.</summary>
public enum LierneStateSection
{
    /// <summary>Settings and keyboard shortcuts.</summary>
    Preferences,

    /// <summary>Documents, tools, and the shell layout.</summary>
    Workspace,

    /// <summary>Regenerable data that may be discarded without losing user work.</summary>
    Cache
}

/// <summary>Reads and atomically replaces a host's versioned Lierne state documents.</summary>
public interface ILierneStateStorage
{
    /// <summary>Reads a section, or returns null when it has never been stored.</summary>
    ValueTask<string?> ReadAsync(LierneStateSection section, CancellationToken cancellationToken = default);

    /// <summary>Replaces the stored JSON for one section.</summary>
    ValueTask WriteAsync(LierneStateSection section, string json, CancellationToken cancellationToken = default);
}

/// <summary>Lets a plugin serialize and migrate the payload of its persistent documents.</summary>
/// <param name="Version">The positive schema version written by the current plugin.</param>
/// <param name="Write">Produces the JSON for one payload.</param>
/// <param name="Read">Restores a payload using the version stored with it.</param>
public sealed record LierneDocumentStateCodec(
    int Version,
    Func<object?, JsonElement> Write,
    Func<int, JsonElement, object?> Read);

/// <summary>Converts a stored state section from a host's legacy schema to the next version.</summary>
/// <param name="Section">The section this migration owns.</param>
/// <param name="FromVersion">The version to upgrade.</param>
/// <param name="Upgrade">Updates the JSON fields. Lierne advances the envelope version.</param>
public sealed record LierneStateMigration(LierneStateSection Section, int FromVersion, Action<JsonObject> Upgrade);
