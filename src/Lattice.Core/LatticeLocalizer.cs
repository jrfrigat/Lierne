using System.Globalization;

namespace Lattice;

/// <summary>Merges the plugin string catalogs and resolves keys by culture.</summary>
public sealed class LatticeLocalizer : ILatticeLocalizer
{
    private const string FallbackCulture = "en";
    private readonly Dictionary<string, Dictionary<string, string>> _catalogs =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly string _fallbackCulture;

    /// <summary>Creates the localizer for the current UI culture.</summary>
    /// <param name="catalog">The composed plugin catalog.</param>
    public LatticeLocalizer(LatticePluginCatalog catalog)
        : this(catalog, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)
    {
    }

    /// <summary>Creates the localizer for a fixed culture.</summary>
    /// <param name="catalog">The composed plugin catalog.</param>
    /// <param name="culture">The initial two-letter culture.</param>
    public LatticeLocalizer(LatticePluginCatalog catalog, string culture)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        foreach (var entry in catalog.StringCatalogs)
        {
            if (!_catalogs.TryGetValue(entry.Culture, out var strings))
            {
                strings = new Dictionary<string, string>(StringComparer.Ordinal);
                _catalogs[entry.Culture] = strings;
            }

            foreach (var pair in entry.Strings)
            {
                strings[pair.Key] = pair.Value;
            }
        }

        CurrentCulture = culture;
        _fallbackCulture = _catalogs.ContainsKey(FallbackCulture) ? FallbackCulture : culture;
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public string CurrentCulture { get; private set; }

    /// <inheritdoc />
    public string Get(StringKey key)
    {
        var text = key.ToString();
        if (TryGet(CurrentCulture, text, out var value))
        {
            return value;
        }

        return TryGet(_fallbackCulture, text, out value) ? value : text;
    }

    /// <inheritdoc />
    public void SetCulture(string culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        if (string.Equals(CurrentCulture, culture, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CurrentCulture = culture;
        Changed?.Invoke();
    }

    private bool TryGet(string culture, string key, out string value)
    {
        if (_catalogs.TryGetValue(culture, out var strings) &&
            strings.TryGetValue(key, out var text))
        {
            value = text;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
