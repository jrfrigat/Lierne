using System.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace Lierne.Core.Tests;

/// <summary>Tests plugin ordering, contribution collection and composition diagnostics.</summary>
public sealed class LiernePluginCatalogTests
{
    [Fact]
    public void OrdersPluginsByDependenciesBeforeDeclaration()
    {
        var dependent = new TestPlugin(
            "app.connection",
            builder => builder.AddCommand(
                TestDescriptors.Command("app.connection.connect"),
                TestDescriptors.NoOp),
            "app.core");
        var core = new TestPlugin(
            "app.core",
            builder => builder.AddCommand(
                TestDescriptors.Command("app.core.open"),
                TestDescriptors.NoOp));

        var catalog = LiernePluginCatalog.Build([dependent, core]);

        Assert.False(catalog.HasErrors);
        Assert.Equal(["app.core", "app.connection"], catalog.Plugins.Select(plugin => plugin.Id.Value));
        Assert.Equal(
            ["app.core.open", "app.connection.connect"],
            catalog.Commands.Select(command => command.Id.Value));
    }

    [Fact]
    public void ReportsDuplicatePluginIds()
    {
        var catalog = LiernePluginCatalog.Build(
            [new TestPlugin("app.same"), new TestPlugin("app.same")]);

        Assert.True(catalog.HasErrors);
        Assert.Contains(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "lattice.plugin.duplicate");
    }

    [Fact]
    public void ReportsMissingDependencies()
    {
        var catalog = LiernePluginCatalog.Build(
            [new TestPlugin("app.connection", "app.missing")]);

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
        var catalog = LiernePluginCatalog.Build(
        [
            new TestPlugin("app.a", "app.b"),
            new TestPlugin("app.b", "app.a")
        ]);

        Assert.True(catalog.HasErrors);
        Assert.Contains(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "lattice.plugin.cycle");
    }

    [Fact]
    public void ReportsSettingsSectionsDeclaredByTwoPlugins()
    {
        static SettingsTransferDescriptor Section(string id) => new(
            id,
            TestDescriptors.Key(id),
            _ => null,
            (_, _) => LierneResult.Ok);

        var catalog = LiernePluginCatalog.Build(
        [
            new TestPlugin(
                "app.one",
                builder => builder.AddSettingsTransfer(Section("app.settings"))),
            new TestPlugin(
                "app.two",
                builder => builder.AddSettingsTransfer(Section("app.settings")))
        ]);

        Assert.True(catalog.HasErrors);
        Assert.Contains(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "lattice.contribution.duplicate");
    }

    [Fact]
    public void ReportsShellContentDeclaredByTwoPlugins()
    {
        static ShellContentDescriptor Content(string id) => new(
            new ShellContentId(id),
            LierneShellSlot.MenuBar,
            typeof(object));

        var catalog = LiernePluginCatalog.Build(
        [
            new TestPlugin(
                "app.one",
                builder => builder.AddShellContent(Content("app.menubar"))),
            new TestPlugin(
                "app.two",
                builder => builder.AddShellContent(Content("app.menubar")))
        ]);

        Assert.True(catalog.HasErrors);
        Assert.Contains(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "lattice.contribution.duplicate");
    }

    [Fact]
    public void ReportsDuplicateCommandIdsAcrossPlugins()
    {
        var catalog = LiernePluginCatalog.Build(
        [
            new TestPlugin(
                "app.one",
                builder => builder.AddCommand(
                    TestDescriptors.Command("app.shared.run"),
                    TestDescriptors.NoOp)),
            new TestPlugin(
                "app.two",
                builder => builder.AddCommand(
                    TestDescriptors.Command("app.shared.run"),
                    TestDescriptors.NoOp))
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
        var catalog = LiernePluginCatalog.Build(
        [
            new TestPlugin(
                "app.ribbon",
                builder =>
                {
                    builder.AddRibbonGroup(new RibbonGroupDescriptor(
                        new RibbonGroupId("app.group"),
                        TestDescriptors.Key("group")));
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
        var catalog = LiernePluginCatalog.Build(
        [
            new TestPlugin(
                "app.ribbon",
                builder =>
                {
                    builder.AddCommand(
                        TestDescriptors.Command("app.run"),
                        TestDescriptors.NoOp);
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
        var catalog = LiernePluginCatalog.Build(
        [
            new TestPlugin(
                "app.feature",
                builder =>
                {
                    builder.AddDocument(new DocumentDescriptor(
                        new DocumentTypeId("app.document"),
                        TestDescriptors.Key("document"),
                        null,
                        typeof(object)));
                    builder.AddToolWindow(new ToolWindowDescriptor(
                        new ToolWindowId("app.tool"),
                        TestDescriptors.Key("tool"),
                        "tool",
                        typeof(object)));
                    builder.AddCommand(
                        TestDescriptors.Command("app.run"),
                        TestDescriptors.NoOp);
                    builder.AddRibbonGroup(new RibbonGroupDescriptor(
                        new RibbonGroupId("app.group"),
                        TestDescriptors.Key("group")));
                    builder.AddRibbonItem(new RibbonItemDescriptor(
                        new RibbonItemId("app.item"),
                        new RibbonGroupId("app.group"),
                        new CommandId("app.run")));
                    builder.AddSettingsPage(new SettingsPageDescriptor(
                        new SettingsPageId("app.settings"),
                        TestDescriptors.Key("settings")));                    builder.AddSetting(new SettingDescriptor(
                        new SettingsKey("app.timeout"),
                        TestDescriptors.Key("timeout"),
                        LierneSettingKind.Integer,
                        30));
                    builder.AddMenuGroup(new MenuGroupDescriptor(
                        new MenuGroupId("app.tools"),
                        TestDescriptors.Key("tools")));
                    builder.AddMenuItem(new MenuItemDescriptor(
                        new MenuItemId("app.tools.run"),
                        new MenuGroupId("app.tools"),
                        TestDescriptors.Key("run"),
                        new CommandId("app.run")));
                    builder.AddStrings(new LierneStringCatalog(
                        "tests",
                        TestStrings.ResourceManager));
                    builder.AddToolbar(new ToolbarDescriptor(
                        new ToolbarId("app.toolbar"),
                        typeof(object)));
                    builder.AddStatusBarItem(new StatusBarItemDescriptor(
                        new StatusBarItemId("app.status"),
                        TestDescriptors.Key("status")));
                })
        ]);

        Assert.False(catalog.HasErrors);
        Assert.Single(catalog.Documents);
        Assert.Single(catalog.ToolWindows);
        Assert.Single(catalog.Commands);
        Assert.Equal(
            new PluginId("app.feature"),
            Assert.Single(catalog.CommandRegistrations).Plugin);
        Assert.Single(catalog.CommandRegistrations);
        Assert.Single(catalog.RibbonGroups);
        Assert.Single(catalog.RibbonItems);
        Assert.Single(catalog.SettingsPages);
        Assert.Equal(
            new PluginId("app.feature"),
            Assert.Single(catalog.SettingsPages).Plugin);
        Assert.Equal(
            LierneSettingsGroup.Plugin,
            Assert.Single(catalog.SettingsPages).Group);
        Assert.Single(catalog.Settings);
        Assert.Single(catalog.MenuGroups);
        Assert.Single(catalog.MenuItems);
        Assert.Single(catalog.StringCatalogs);
        Assert.Single(catalog.Toolbars);
        Assert.Single(catalog.StatusBarItems);
    }

    [Fact]
    public void ReportsMenuItemsThatReferenceUnknownGroups()
    {
        var catalog = LiernePluginCatalog.Build(
        [
            new TestPlugin(
                "app.menu",
                builder => builder.AddMenuItem(new MenuItemDescriptor(
                    new MenuItemId("app.item"),
                    new MenuGroupId("app.missing"),
                    TestDescriptors.Key("item"),
                    new CommandId("app.run"))))
        ]);

        Assert.True(catalog.HasErrors);
        Assert.Contains(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "lattice.menu.unknown_group");
    }

    [Fact]
    public void ReportsMenuItemsWithUnknownCommands()
    {
        var catalog = LiernePluginCatalog.Build(
        [
            new TestPlugin(
                "app.menu",
                builder =>
                {
                    builder.AddMenuGroup(new MenuGroupDescriptor(
                        new MenuGroupId("app.tools"),
                        TestDescriptors.Key("tools")));
                    builder.AddMenuItem(new MenuItemDescriptor(
                        new MenuItemId("app.item"),
                        new MenuGroupId("app.tools"),
                        TestDescriptors.Key("item"),
                        new CommandId("app.missing")));
                })
        ]);

        Assert.True(catalog.HasErrors);
        Assert.Contains(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "lattice.menu.unknown_command");
    }

    [Fact]
    public void ReportsLeafMenuItemsWithoutCommands()
    {
        var catalog = LiernePluginCatalog.Build(
        [
            new TestPlugin(
                "app.menu",
                builder =>
                {
                    builder.AddMenuGroup(new MenuGroupDescriptor(
                        new MenuGroupId("app.tools"),
                        TestDescriptors.Key("tools")));
                    builder.AddMenuItem(new MenuItemDescriptor(
                        new MenuItemId("app.item"),
                        new MenuGroupId("app.tools"),
                        TestDescriptors.Key("item")));
                })
        ]);

        Assert.True(catalog.HasErrors);
        Assert.Contains(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "lattice.menu.invalid_item");
    }

    [Fact]
    public void ReportsSettingsWithoutAPage()
    {
        var catalog = LiernePluginCatalog.Build(
        [
            new TestPlugin(
                "app.settings",
                builder => builder.AddSetting(new SettingDescriptor(
                    new SettingsKey("app.timeout"),
                    TestDescriptors.Key("timeout"),
                    LierneSettingKind.Integer,
                    30,
                    Page: new SettingsPageId("app.missing"))))
        ]);

        Assert.True(catalog.HasErrors);
        Assert.Contains(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "lattice.settings.unknown_page");
    }

    [Fact]
    public void RejectsGeneratedActionsThatCannotSupplyACommandArgument()
    {
        var command = new CommandId("app.typed");
        var menu = new MenuGroupId("app.menu");
        var ribbon = new RibbonGroupId("app.ribbon");
        var catalog = LiernePluginCatalog.Build([new TestPlugin("app.typed", builder =>
        {
            builder.AddCommand(TestDescriptors.Command(command.Value, typeof(string)), TestDescriptors.NoOp);
            builder.AddMenuGroup(new MenuGroupDescriptor(menu, TestDescriptors.Key("menu")));
            builder.AddMenuItem(new MenuItemDescriptor(new("app.menu.item"), menu, TestDescriptors.Key("item"), command));
            builder.AddRibbonGroup(new RibbonGroupDescriptor(ribbon, TestDescriptors.Key("ribbon")));
            builder.AddRibbonItem(new RibbonItemDescriptor(new("app.ribbon.item"), ribbon, command));
            builder.AddStatusBarItem(new StatusBarItemDescriptor(new("app.status"), TestDescriptors.Key("status"), Command: command));
        })]);

        Assert.Equal(3, catalog.Diagnostics.Count(diagnostic => diagnostic.Code == "lattice.command.argument_required"));
    }

    [Fact]
    public void RejectsASettingDefaultOutsideItsDeclaredRange()
    {
        var catalog = LiernePluginCatalog.Build([new TestPlugin("app.settings", builder =>
            builder.AddSetting(new SettingDescriptor(
                new SettingsKey("app.size"), TestDescriptors.Key("size"),
                LierneSettingKind.Integer, DefaultValue: 6, Max: 5)))]);

        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "lattice.settings.invalid_default");
    }

    [Fact]
    public void ReportsStringNamespacesServedByTwoPlugins()
    {
        var catalog = LiernePluginCatalog.Build(
        [
            new TestPlugin(
                "app.one",
                builder => builder.AddStrings(new LierneStringCatalog(
                    "app",
                    TestStrings.ResourceManager))),
            new TestPlugin(
                "app.two",
                builder => builder.AddStrings(new LierneStringCatalog(
                    "app",
                    TestStrings.ResourceManager)))
        ]);

        Assert.True(catalog.HasErrors);
        Assert.Contains(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "lattice.strings.duplicate_namespace");
    }

    [Fact]
    public void ReportsStringCatalogsWithoutANeutralManifest()
    {
        var missing = new ResourceManager(
            "Lierne.Core.Tests.Resources.DoesNotExist",
            typeof(TestStrings).Assembly);
        var catalog = LiernePluginCatalog.Build(
        [
            new TestPlugin(
                "app.missing",
                builder => builder.AddStrings(new LierneStringCatalog("app", missing)))
        ]);

        Assert.True(catalog.HasErrors);
        Assert.Contains(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "lattice.strings.missing_manifest");
    }

    [Fact]
    public void AddLierneRegistersTheCatalogAndPluginServices()
    {
        var services = new ServiceCollection();
        services.AddLierne(options => options.AddPlugin(
            new TestPlugin(
                "app.feature",
                builder => builder.Services.AddSingleton<PluginService>())));

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<LiernePluginCatalog>());
        Assert.NotNull(provider.GetRequiredService<PluginService>());
    }

    [Fact]
    public void AddLierneThrowsWhenTheCompositionIsInvalid()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<LierneCompositionException>(() =>
            services.AddLierne(options => options.AddPlugin(
                new TestPlugin("app.connection", "app.missing"))));

        Assert.Contains(
            exception.Diagnostics,
            diagnostic => diagnostic.Code == "lattice.plugin.missing_dependency");
    }

    private sealed class PluginService
    {
    }
}
