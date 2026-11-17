using System.Globalization;

namespace Lattice;

/// <summary>Resolves string keys through the resource catalogs contributed by the plugins.</summary>
public sealed class LatticeLocalizer : ILatticeLocalizer
{
    private readonly IReadOnlyList<LatticeStringCatalog> _catalogs;

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
        _catalogs = catalog.StringCatalogs;
        CurrentCulture = culture;
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public string CurrentCulture { get; private set; }

    /// <inheritdoc />
    public string Get(StringKey key)
    {
        var culture = CultureInfo.GetCultureInfo(CurrentCulture);
        foreach (var catalog in _catalogs)
        {
            if (!string.Equals(catalog.Namespace, key.Namespace, StringComparison.Ordinal))
            {
                continue;
            }

            // The resource manager falls back from the satellite culture to the neutral resources,
            // which hold the English text.
            if (catalog.Resources.GetString(key.Name, culture) is { } value)
            {
                return value;
            }
        }

        return key.ToString();
    }

    /// <inheritdoc />
    public string GetError(string code, string? fallback = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var culture = CultureInfo.GetCultureInfo(CurrentCulture);
        if (LatticeCoreStrings.ResourceManager.GetString(code, culture) is { } core)
        {
            return core;
        }

        foreach (var catalog in _catalogs)
        {
            if (catalog.Resources.GetString(code, culture) is { } value)
            {
                return value;
            }
        }

        return string.IsNullOrWhiteSpace(fallback) ? code : fallback;
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
}
