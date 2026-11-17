using System.Globalization;
using Flare.Abstractions;
using Microsoft.Extensions.Localization;

namespace Lattice.Components.Tests;

/// <summary>A localization engine that returns every key as its own text.</summary>
internal sealed class TestLocalization : IFlareLocalization
{
    private readonly CultureInfo _culture;

    public TestLocalization()
        : this(CultureInfo.InvariantCulture)
    {
    }

    public TestLocalization(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        _culture = culture;
    }

    public CultureInfo Culture => _culture;

    public CultureInfo UICulture => _culture;

    public event Action? Changed
    {
        add { }
        remove { }
    }

    public void SetCulture(CultureInfo culture, CultureInfo? uiCulture)
    {
    }

    public IStringLocalizer<TResource> GetLocalizer<TResource>() =>
        new TestLocalizer<TResource>();

    private sealed class TestLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
