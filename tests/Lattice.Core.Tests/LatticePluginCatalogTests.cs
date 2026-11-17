using Microsoft.Extensions.DependencyInjection;

namespace Lattice.Core.Tests;

/// <summary>Tests plugin ordering, contribution collection and composition diagnostics.</summary>
public sealed class LatticePluginCatalogTests
{
    [Fact]
    public void OrdersPluginsByDependenciesBeforeDeclaration()
    {
        var dependent = Plugin(
            "app.connection",
            builder => builder.AddCommand(Command("app.connection.connect")),
            "app.core");
        var core = Plugin(
            "app.core",
            builder => builder.AddCommand(Command("app.core.open")));

        var catalog = LatticePluginCatalog.Build([dependent, core]);

        Assert.False(catalog.HasErrors);
        Assert.Equal(["app.core", "app.connection"], catalog.Plugins.Select(plugin => plugin.Id.Value));
        Assert.Equal(
            ["app.core.open", "app.connection.connect"],
            catalog.Commands.Select(command => command.Id.Value));
    }

    [Fact]
    public void ReportsDuplicatePluginIds()
    {
        var catalog = LatticePluginCatalog.Build([Plugin("app.same"), Plugin("app.same")]);

        Assert.True(catalog.HasErrors);
        Assert.Contains(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "lattice.plugin.duplicate");
    }

    [Fact]
    public void ReportsMissingDependencies()
    {
        var catalog = LatticePluginCatalog.Build([Plugin("app.connection", "app.missing")]);

        Assert.True(catalog.HasErrors);
        Assert.Contains(
            catalog.Diagnostics,
            diagnostic =>
                diagnostic.Code == "lattice.plugin.missing_dependency" &&
                diagnostic.Message.Contains("app.missing", StringComparison.Ordinal));
    }

    [Fact]
    public void ReportsDependencyCycles()
    {
        var catalog = LatticePluginCatalog.Build(
        [
            Plugin("app.a", "app.b"),
            Plugin("app.b", "app.a")
        ]);

        Assert.True(catalog.HasErrors);
        Assert.Contains(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "lattice.plugin.cycle");
    }

    [Fact]
    public void ReportsDuplicateCommandIdsAcrossPlugins()
    {
        var catalog = LatticePluginCatalog.Build(
        [
            Plugin("app.one", builder => builder.AddCommand(Command("app.shared.run"))),
            Plugin("app.two", builder => builder.AddCommand(Command("app.shared.run")))
        ]);

        Assert.True(catalog.HasErrors);
        var duplicate = Assert.Single(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "lattice.contribution.duplicate");
        Assert.Contains("app.one", duplicate.Message, StringComparison.Ordinal);
        Assert.Contains("app.two", duplicate.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsRibbonItemsThatInvokeUnknownCommands()
    {
        var catalog = LatticePluginCatalog.Build(
        [
            Plugin(
                "app.ribbon",
                builder =>
                {
                    builder.AddRibbonGroup(new RibbonGroupDescriptor(
                        new RibbonGroupId("app.group"),
                        Key("group")));
                    builder.AddRibbonItem(new RibbonItemDescriptor(
                        new RibbonItemId("app.item"),
                        new RibbonGroupId("app.group"),
                        new CommandId("app.missing")));
                })
        ]);

        Assert.True(catalog.HasErrors);
        Assert.Contains(
            catalog.Diagnostics,
            diagnostic =>
                diagnostic.Code == "lattice.ribbon.unknown_command" &&
                diagnostic.Message.Contains("app.missing", StringComparison.Ordinal));
    }

    [Fact]
    public void ReportsRibbonItemsThatBelongToUnknownGroups()
    {
        var catalog = LatticePluginCatalog.Build(
        [
            Plugin(
                "app.ribbon",
                builder =>
                {
                    builder.AddCommand(Command("app.run"));
                    builder.AddRibbonItem(new RibbonItemDescriptor(
                        new RibbonItemId("app.item"),
                        new RibbonGroupId("app.missing"),
                        new CommandId("app.run")));
                })
        ]);

        Assert.True(catalog.HasErrors);
        Assert.Contains(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "lattice.ribbon.unknown_group");
    }

    [Fact]
    public void CollectsEveryContributionKind()
    {
        var catalog = LatticePluginCatalog.Build(
        [
            Plugin(
                "app.feature",
                builder =>
                {
                    builder.AddDocument(new DocumentDescriptor(
                        new DocumentTypeId("app.document"),
                        Key("document"),
                        null,
                        typeof(object)));
                    builder.AddToolWindow(new ToolWindowDescriptor(
                        new ToolWindowId("app.tool"),
                        Key("tool"),
                        "tool",
                        typeof(object)));
                    builder.AddCommand(Command("app.run"));
                    builder.AddRibbonGroup(new RibbonGroupDescriptor(
                        new RibbonGroupId("app.group"),
                        Key("group")));
                    builder.AddRibbonItem(new RibbonItemDescriptor(
                        new RibbonItemId("app.item"),
                        new RibbonGroupId("app.group"),
                        new CommandId("app.run")));
                    builder.AddSettingsPage(new SettingsPageDescriptor(
                        new SettingsPageId("app.settings"),
                        Key("settings")));
                    builder.AddSetting(new SettingDescriptor(
                        new SettingsKey("app.timeout"),
                        Key("timeout"),
                        LatticeSettingKind.Integer,
                        30));
                })
        ]);

        Assert.False(catalog.HasErrors);
        Assert.Single(catalog.Documents);
        Assert.Single(catalog.ToolWindows);
        Assert.Single(catalog.Commands);
        Assert.Single(catalog.RibbonGroups);
        Assert.Single(catalog.RibbonItems);
        Assert.Single(catalog.SettingsPages);
        Assert.Single(catalog.Settings);
    }

    [Fact]
    public void AddLatticeRegistersTheCatalogAndPluginServices()
    {
        var services = new ServiceCollection();
        services.AddLattice(options => options.AddPlugin(
            Plugin(
                "app.feature",
                builder => builder.Services.AddSingleton<PluginService>())));

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<LatticePluginCatalog>());
        Assert.NotNull(provider.GetRequiredService<PluginService>());
    }

    [Fact]
    public void AddLatticeThrowsWhenTheCompositionIsInvalid()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<LatticeCompositionException>(() =>
            services.AddLattice(options => options.AddPlugin(Plugin("app.connection", "app.missing"))));

        Assert.Contains(
            exception.Diagnostics,
            diagnostic => diagnostic.Code == "lattice.plugin.missing_dependency");
    }

    private static TestPlugin Plugin(string id, params string[] dependsOn) =>
        new(id, null, dependsOn);

    private static TestPlugin Plugin(
        string id,
        Action<ILatticePluginBuilder> configure,
        params string[] dependsOn) =>
        new(id, configure, dependsOn);

    private static CommandDescriptor Command(string id) =>
        new(new CommandId(id), Key(id));

    private static StringKey Key(string name) => new("tests", name);

    private sealed class TestPlugin : ILatticePlugin
    {
        private readonly Action<ILatticePluginBuilder>? _configure;

        public TestPlugin(
            string id,
            Action<ILatticePluginBuilder>? configure,
            string[] dependsOn)
        {
            Id = new PluginId(id);
            DisplayName = new StringKey("tests", id);
            DependsOn = dependsOn.Select(value => new PluginId(value)).ToArray();
            _configure = configure;
        }

        public PluginId Id { get; }

        public StringKey DisplayName { get; }

        public IReadOnlyList<PluginId> DependsOn { get; }

        public void Configure(ILatticePluginBuilder builder) => _configure?.Invoke(builder);
    }

    private sealed class PluginService
    {
    }
}
