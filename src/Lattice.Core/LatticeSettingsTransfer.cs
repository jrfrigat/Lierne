using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lattice;

/// <summary>
/// Builds the JSON document that carries the plugin settings sections and applies such a document
/// back to the plugins.
/// </summary>
/// <remarks>
/// The document has the shape
/// <c>{ "format": "lattice.settings", "version": 1, "sections": { "&lt;section id&gt;": ... } }</c>.
/// The envelope and the validation belong to Lattice; the value of each section belongs to the plugin
/// that declared it. An import snapshots every participating section before it applies anything, so a
/// failure restores the values already changed by the same document.
/// </remarks>
public sealed class LatticeSettingsTransfer
{
    /// <summary>The value of the document <c>format</c> property.</summary>
    public const string FormatName = "lattice.settings";

    /// <summary>The document format version this host reads and writes.</summary>
    public const int FormatVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly LatticePluginCatalog _catalog;

    /// <summary>Creates the transfer over the composed plugins.</summary>
    /// <param name="catalog">The composed plugin catalog.</param>
    public LatticeSettingsTransfer(LatticePluginCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    /// <summary>Gets the sections in plugin order.</summary>
    public IReadOnlyList<SettingsTransferDescriptor> Sections => _catalog.SettingsTransfers;

    /// <summary>Builds the JSON document from the current settings.</summary>
    /// <param name="services">The application services the sections read their state from.</param>
    /// <returns>The document text, or a failure that names the section that could not be exported.</returns>
    public LatticeResult<string> Export(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var sections = new JsonObject();
        foreach (var descriptor in _catalog.SettingsTransfers)
        {
            try
            {
                if (descriptor.Export(services) is { } value)
                {
                    sections[descriptor.Section] = value.DeepClone();
                }
            }
            catch (Exception exception)
            {
                return LatticeResult<string>.Fail(
                    "lattice.settings.export_failed",
                    $"The section '{descriptor.Section}' could not be exported: {exception.Message}");
            }
        }

        var document = new JsonObject
        {
            ["format"] = FormatName,
            ["version"] = FormatVersion,
            ["sections"] = sections
        };
        return LatticeResult<string>.Ok(document.ToJsonString(SerializerOptions));
    }

    /// <summary>Validates and applies a settings document.</summary>
    /// <param name="json">The document text.</param>
    /// <param name="services">The application services the sections write their state through.</param>
    /// <returns>The outcome; a failure names the stable error code and leaves the settings as they were.</returns>
    public LatticeResult Import(string json, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(json))
        {
            return LatticeResult.Fail(
                "lattice.settings.empty",
                "The settings document is empty.");
        }

        JsonObject document;
        try
        {
            if (JsonNode.Parse(json) is not JsonObject parsed)
            {
                return LatticeResult.Fail(
                    "lattice.settings.invalid_format",
                    "The settings document is not a JSON object.");
            }

            document = parsed;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return LatticeResult.Fail(
                "lattice.settings.invalid_json",
                $"The settings document is not valid JSON: {exception.Message}");
        }

        if (!string.Equals(ReadString(document, "format"), FormatName, StringComparison.Ordinal))
        {
            return LatticeResult.Fail(
                "lattice.settings.invalid_format",
                $"The settings document is not a '{FormatName}' document.");
        }

        if (ReadInt32(document, "version") is not { } version || version != FormatVersion)
        {
            return LatticeResult.Fail(
                "lattice.settings.unsupported_version",
                $"The settings document version is not supported; this build reads version {FormatVersion}.");
        }

        if (!document.TryGetPropertyValue("sections", out var sectionsNode) ||
            sectionsNode is not JsonObject sections)
        {
            return LatticeResult.Fail(
                "lattice.settings.missing_sections",
                "The settings document has no sections object.");
        }

        var known = _catalog.SettingsTransfers.ToDictionary(
            descriptor => descriptor.Section,
            StringComparer.Ordinal);
        try
        {
            foreach (var section in sections)
            {
                if (!known.ContainsKey(section.Key))
                {
                    return LatticeResult.Fail(
                        "lattice.settings.unknown_section",
                        $"Section '{section.Key}' is not declared by any plugin.");
                }
            }
        }
        catch (ArgumentException exception)
        {
            return LatticeResult.Fail(
                "lattice.settings.duplicate_section",
                $"The settings document repeats a section: {exception.Message}");
        }

        var snapshots = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        var applied = new List<SettingsTransferDescriptor>();
        foreach (var descriptor in _catalog.SettingsTransfers)
        {
            if (!sections.TryGetPropertyValue(descriptor.Section, out var value))
            {
                continue;
            }

            snapshots[descriptor.Section] = Snapshot(descriptor, services);
            LatticeResult result;
            try
            {
                result = descriptor.Import(services, value?.DeepClone());
            }
            catch (Exception exception)
            {
                result = LatticeResult.Fail(
                    "lattice.settings.import_failed",
                    $"The section '{descriptor.Section}' could not be imported: {exception.Message}");
            }

            if (!result.Success)
            {
                Restore(applied, snapshots, services);
                return LatticeResult.Fail(
                    result.ErrorCode ?? "lattice.settings.import_failed",
                    result.ErrorMessage ?? "The settings document could not be imported.");
            }

            applied.Add(descriptor);
        }

        return LatticeResult.Ok;
    }

    private static JsonNode? Snapshot(
        SettingsTransferDescriptor descriptor,
        IServiceProvider services)
    {
        try
        {
            return descriptor.Export(services)?.DeepClone();
        }
        catch
        {
            return null;
        }
    }

    private static void Restore(
        IReadOnlyList<SettingsTransferDescriptor> applied,
        IReadOnlyDictionary<string, JsonNode?> snapshots,
        IServiceProvider services)
    {
        for (var index = applied.Count - 1; index >= 0; index--)
        {
            var descriptor = applied[index];
            snapshots.TryGetValue(descriptor.Section, out var snapshot);
            try
            {
                descriptor.Import(services, snapshot?.DeepClone());
            }
            catch
            {
                // A failing rollback is best effort; the import result already reports the failure.
            }
        }
    }

    private static string? ReadString(JsonObject document, string name) =>
        document.TryGetPropertyValue(name, out var node) &&
        node is JsonValue value &&
        value.TryGetValue<string>(out var text)
            ? text
            : null;

    private static int? ReadInt32(JsonObject document, string name) =>
        document.TryGetPropertyValue(name, out var node) &&
        node is JsonValue value &&
        value.TryGetValue<int>(out var number)
            ? number
            : null;
}
