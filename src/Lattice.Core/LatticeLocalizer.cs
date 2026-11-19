using System.Globalization;

namespace Lattice;

/// <summary>Resolves string keys through the resource catalogs contributed by the plugins.</summary>
public sealed class LatticeLocalizer : ILatticeLocalizer
{
    private readonly IReadOnlyList<LatticeStringCatalog> _catalogs;
    private CultureInfo? _culture;

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
        if (string.Equals(key.Namespace, StringKey.LiteralNamespace, StringComparison.Ordinal))
        {
            return key.Name;
        }

        var culture = _culture ??= CultureInfo.GetCultureInfo(CurrentCulture);
        for (var index = 0; index < _catalogs.Count; index++)
        {
            var catalog = _catalogs[index];
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
        var culture = _culture ??= CultureInfo.GetCultureInfo(CurrentCulture);
        if (LatticeCoreStrings.ResourceManager.GetString(code, culture) is { } core)
        {
            return core;
        }

        for (var index = 0; index < _catalogs.Count; index++)
        {
            if (_catalogs[index].Resources.GetString(code, culture) is { } value)
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
        _culture = null;
        Changed?.Invoke();
    }
}
