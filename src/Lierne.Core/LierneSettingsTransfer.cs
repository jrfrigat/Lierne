using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lierne;

/// <summary>
/// Builds the JSON document that carries the plugin settings sections and applies such a document
/// back to the plugins.
/// </summary>
/// <remarks>
/// The document has the shape
/// <c>{ "format": "lattice.settings", "version": 1, "sections": { "&lt;section id&gt;": ... } }</c>.
/// The envelope and the validation belong to Lierne; the value of each section belongs to the plugin
/// that declared it. An import snapshots every participating section before it applies anything, so a
/// failure can restore the values already changed by the same document. A failed restoration is
/// reported separately.
/// </remarks>
public sealed class LierneSettingsTransfer
{
    /// <summary>The value of the document <c>format</c> property.</summary>
    public const string FormatName = "lattice.settings";

    /// <summary>The document format version this host reads and writes.</summary>
    public const int FormatVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly LiernePluginCatalog _catalog;

    /// <summary>Creates the transfer over the composed plugins.</summary>
    /// <param name="catalog">The composed plugin catalog.</param>
    public LierneSettingsTransfer(LiernePluginCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    /// <summary>Gets the sections in plugin order.</summary>
    public IReadOnlyList<SettingsTransferDescriptor> Sections => _catalog.SettingsTransfers;

    /// <summary>Builds the JSON document from the current settings.</summary>
    /// <param name="services">The application services the sections read their state from.</param>
    /// <returns>The document text, or a failure that names the section that could not be exported.</returns>
    public LierneResult<string> Export(IServiceProvider services)
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
                return LierneResult<string>.Fail(
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
        return LierneResult<string>.Ok(document.ToJsonString(SerializerOptions));
    }

    /// <summary>Validates and applies a settings document.</summary>
    /// <param name="json">The document text.</param>
    /// <param name="services">The application services the sections write their state through.</param>
    /// <returns>The outcome, including a separate error if the previous settings could not be restored.</returns>
    public LierneResult Import(string json, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(json))
        {
            return LierneResult.Fail(
                "lattice.settings.empty",
                "The settings document is empty.");
        }

        JsonDocument parsed;
        try
        {
            parsed = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            return LierneResult.Fail(
                "lattice.settings.invalid_json",
                $"The settings document is not valid JSON: {exception.Message}");
        }

        using var input = parsed;
        var document = input.RootElement;
        if (document.ValueKind != JsonValueKind.Object)
        {
            return LierneResult.Fail(
                "lattice.settings.invalid_format",
                "The settings document is not a JSON object.");
        }

        if (FindDuplicateProperty(document) is { } property)
        {
            return LierneResult.Fail(
                "lattice.settings.duplicate_property",
                $"The settings document repeats the property '{property}'.");
        }

        if (!document.TryGetProperty("format", out var format) ||
            format.ValueKind != JsonValueKind.String ||
            !string.Equals(format.GetString(), FormatName, StringComparison.Ordinal))
        {
            return LierneResult.Fail(
                "lattice.settings.invalid_format",
                $"The settings document is not a '{FormatName}' document.");
        }

        if (!document.TryGetProperty("version", out var version) ||
            version.ValueKind != JsonValueKind.Number ||
            !version.TryGetInt32(out var versionNumber) || versionNumber != FormatVersion)
        {
            return LierneResult.Fail(
                "lattice.settings.unsupported_version",
                $"The settings document version is not supported; this build reads version {FormatVersion}.");
        }

        if (!document.TryGetProperty("sections", out var sections) ||
            sections.ValueKind != JsonValueKind.Object)
        {
            return LierneResult.Fail(
                "lattice.settings.missing_sections",
                "The settings document has no sections object.");
        }

        if (FindDuplicateProperty(sections) is { } duplicate)
        {
            return LierneResult.Fail(
                "lattice.settings.duplicate_section",
                $"The settings document repeats the section '{duplicate}'.");
        }

        var known = _catalog.SettingsTransfers
            .Select(descriptor => descriptor.Section)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var section in sections.EnumerateObject())
        {
            if (!known.Contains(section.Name))
            {
                return LierneResult.Fail(
                    "lattice.settings.unknown_section",
                    $"Section '{section.Name}' is not declared by any plugin.");
            }
        }

        var imports = new List<SectionImport>();
        foreach (var descriptor in _catalog.SettingsTransfers)
        {
            if (!sections.TryGetProperty(descriptor.Section, out var value))
            {
                continue;
            }

            try
            {
                var snapshot = descriptor.Export(services)?.DeepClone();
                imports.Add(new SectionImport(descriptor, JsonNode.Parse(value.GetRawText()), snapshot));
            }
            catch (Exception exception)
            {
                return LierneResult.Fail(
                    "lattice.settings.snapshot_failed",
                    $"The section '{descriptor.Section}' could not be prepared for import: {exception.Message}");
            }
        }

        for (var index = 0; index < imports.Count; index++)
        {
            var entry = imports[index];
            LierneResult result;
            try
            {
                result = entry.Descriptor.Import(services, entry.Value);
            }
            catch (Exception exception)
            {
                // An exception can occur after the section has already changed.
                var failure = LierneResult.Fail(
                    "lattice.settings.import_failed",
                    $"The section '{entry.Descriptor.Section}' could not be imported: {exception.Message}");
                return Restore(imports, index, services, failure);
            }

            // A plugin that rejects its input guarantees its own section was not changed.
            if (!result.Success)
            {
                return Restore(imports, index - 1, services, result);
            }
        }

        return LierneResult.Ok;
    }

    private static LierneResult Restore(
        IReadOnlyList<SectionImport> imports,
        int lastChanged,
        IServiceProvider services,
        LierneResult failure)
    {
        var errors = new List<string>();
        for (var index = lastChanged; index >= 0; index--)
        {
            var entry = imports[index];
            try
            {
                var restored = entry.Descriptor.Import(services, entry.Snapshot);
                if (!restored.Success)
                {
                    errors.Add($"{entry.Descriptor.Section}: {restored.ErrorMessage ?? restored.ErrorCode}");
                }
            }
            catch (Exception exception)
            {
                errors.Add($"{entry.Descriptor.Section}: {exception.Message}");
            }
        }

        var message = failure.ErrorMessage ?? "The settings document could not be imported.";
        return errors.Count == 0
            ? LierneResult.Fail(failure.ErrorCode ?? "lattice.settings.import_failed", message)
            : LierneResult.Fail(
                "lattice.settings.rollback_failed",
                $"{message} The following sections could not be restored: {string.Join("; ", errors)}");
    }

    private static string? FindDuplicateProperty(JsonElement element)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                return property.Name;
            }
        }

        return null;
    }

    private sealed record SectionImport(SettingsTransferDescriptor Descriptor, JsonNode? Value, JsonNode? Snapshot);
}
