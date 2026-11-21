namespace Lierne.Core.Tests;

/// <summary>Tests typed extension points and plugin metadata.</summary>
public sealed class LiernePluginExtensionTests
{
    [Fact]
    public void ExtensionPointsAreTypedAndRemovable()
    {
        var id = new ExtensionPointId("tests.strings");
        var catalog = LiernePluginCatalog.Build(
        [
            new TestPlugin("tests.declarer", builder =>
            {
                builder.AddExtensionPoint<string>(id);
            }),
            new TestPlugin("tests.contributor", builder =>
            {
                builder.GetExtensionPoint<string>(id).Add("value");
            })
        ]);

        var point = catalog.ExtensionPoint<string>(id);
        Assert.Equal(new[] { "value" }, point.Extensions);
        Assert.Empty(catalog.Diagnostics);

        using var subscription = point.Add("second");
        Assert.Equal(new[] { "value", "second" }, point.Extensions);

        subscription.Dispose();
        Assert.Equal(new[] { "value" }, point.Extensions);
    }

    [Fact]
    public void DuplicateExtensionPointProducesDiagnostic()
    {
        var id = new ExtensionPointId("tests.duplicate");
        var catalog = LiernePluginCatalog.Build(
        [
            new TestPlugin("tests.first", builder =>
            {
                builder.AddExtensionPoint<int>(id);
            }),
            new TestPlugin("tests.second", builder =>
            {
                builder.AddExtensionPoint<int>(id);
            })
        ]);

        Assert.Contains(
            catalog.Diagnostics,
            diagnostic =>
                diagnostic.Code == "lattice.extensionpoint.duplicate" &&
                diagnostic.Plugin == new PluginId("tests.second"));
    }

    [Fact]
    public void OptionalDependenciesOrderPluginsWhenPresent()
    {
        var catalog = LiernePluginCatalog.Build(
        [
            new MetadataPlugin("tests.optional", optional: [new PluginId("tests.base")]),
            new MetadataPlugin("tests.base")
        ]);

        Assert.Equal(
            new[] { "tests.base", "tests.optional" },
            catalog.Plugins.Select(plugin => plugin.Id.Value));
        Assert.Empty(catalog.Diagnostics);
    }

    [Fact]
    public void MissingOptionalDependencyIsIgnored()
    {
        var catalog = LiernePluginCatalog.Build(
        [
            new MetadataPlugin("tests.optional", optional: [new PluginId("tests.missing")])
        ]);

        Assert.Single(catalog.Plugins);
        Assert.Empty(catalog.Diagnostics);
    }

    [Fact]
    public void PluginMetadataIsExposed()
    {
        var optional = new[] { new PluginId("tests.optional") };
        var catalog = LiernePluginCatalog.Build(
        [
            new MetadataPlugin(
                "tests.meta",
                optional,
                new Version(1, 2, 3),
                essential: true)
        ]);

        var plugin = Assert.Single(catalog.Plugins);
        Assert.Equal(new Version(1, 2, 3), plugin.Version);
        Assert.True(plugin.Essential);
        Assert.Equal(optional, plugin.OptionalDependsOn);
    }

    private sealed class MetadataPlugin(
        string id,
        IReadOnlyList<PluginId>? optional = null,
        Version? pluginVersion = null,
        bool essential = false) : ILiernePlugin
    {
        public PluginId Id { get; } = new(id);

        public StringKey DisplayName => TestDescriptors.Key(id);

        public IReadOnlyList<PluginId> DependsOn => [];

        public IReadOnlyList<PluginId> OptionalDependsOn { get; } = optional ?? [];

        public Version Version { get; } = pluginVersion ?? new Version(0, 0);

        public bool Essential { get; } = essential;

        public void Configure(ILiernePluginBuilder builder)
        {
        }
    }
}
