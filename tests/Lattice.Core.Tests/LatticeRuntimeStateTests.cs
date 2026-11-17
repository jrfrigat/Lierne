using Microsoft.Extensions.DependencyInjection;

namespace Lattice.Core.Tests;

/// <summary>Tests the context, document, tool-window, settings, localization and shortcut services.</summary>
public sealed class LatticeRuntimeStateTests
{
    [Fact]
    public void TheContextStoresTypedValuesAndNotifiesOnlyOnChanges()
    {
        var state = new LatticeContext();
        var changes = 0;
        state.Changed += () => changes++;

        state.Set("enabled", true);
        state.Set("enabled", true);
        state.Set("count", 3);

        Assert.True(state.Get<bool>("enabled"));
        Assert.Equal(3, state.Get<int>("count"));
        Assert.Equal(2, changes);

        state.Remove("enabled");
        state.Remove("enabled");

        Assert.False(state.TryGet<bool>("enabled", out _));
        Assert.Equal(3, changes);
    }

    [Fact]
    public void DocumentsOpenActivateAndClose()
    {
        var catalog = Build(builder =>
        {
            builder.AddDocument(new DocumentDescriptor(
                new DocumentTypeId("app.one"),
                TestDescriptors.Key("one"),
                null,
                typeof(object)));
            builder.AddDocument(new DocumentDescriptor(
                new DocumentTypeId("app.two"),
                TestDescriptors.Key("two"),
                null,
                typeof(object),
                DefaultLocation: LatticeDockLocation.Right));
        });
        var documents = new LatticeDocumentService(catalog);

        var first = documents.Open(new DocumentTypeId("app.one"));
        var second = documents.Open(
            new DocumentTypeId("app.two"),
            TestDescriptors.Key("override"),
            42);

        Assert.True(first.Success);
        Assert.Equal(TestDescriptors.Key("one"), first.Data!.Title);
        Assert.Equal(LatticeDockLocation.Main, first.Data.Location);
        Assert.True(second.Success);
        Assert.Equal(TestDescriptors.Key("override"), second.Data!.Title);
        Assert.Equal(42, second.Data.Payload);
        Assert.Equal(LatticeDockLocation.Right, second.Data.Location);
        Assert.Same(second.Data, documents.Active);

        Assert.True(documents.Activate(first.Data.Id).Success);
        Assert.Same(first.Data, documents.Active);

        Assert.True(documents.Close(first.Data.Id).Success);
        Assert.Same(second.Data, documents.Active);
        Assert.Single(documents.Documents);

        Assert.True(documents.SetTitle(second.Data.Id, StringKey.Literal("Renamed")).Success);
        Assert.Equal(StringKey.Literal("Renamed"), documents.Active!.Title);

        Assert.True(documents.Move(second.Data.Id, LatticeDockLocation.Left).Success);
        Assert.Equal(LatticeDockLocation.Left, documents.Active!.Location);
        Assert.Equal(LatticeDockLocation.Left, documents.Documents[0].Location);

        Assert.Equal(
            "lattice.document.unknown_type",
            documents.Open(new DocumentTypeId("app.missing")).ErrorCode);
        Assert.Equal(
            "lattice.document.unknown_instance",
            documents.Activate("missing").ErrorCode);
    }

    [Fact]
    public void ToolWindowsShowHideAndToggle()
    {
        var catalog = Build(builder =>
        {
            builder.AddToolWindow(new ToolWindowDescriptor(
                new ToolWindowId("app.explorer"),
                TestDescriptors.Key("explorer"),
                null,
                typeof(object)));
            builder.AddToolWindow(new ToolWindowDescriptor(
                new ToolWindowId("app.output"),
                TestDescriptors.Key("output"),
                null,
                typeof(object),
                LatticeToolRegion.Bottom,
                DefaultVisible: false));
        });
        var toolWindows = new LatticeToolWindowService(catalog);
        var explorer = new ToolWindowId("app.explorer");
        var output = new ToolWindowId("app.output");

        Assert.True(toolWindows.IsVisible(explorer));
        Assert.False(toolWindows.IsVisible(output));
        Assert.Equal(2, toolWindows.ToolWindows.Count);

        Assert.True(toolWindows.Hide(explorer).Success);
        Assert.False(toolWindows.IsVisible(explorer));
        Assert.True(toolWindows.Toggle(explorer).Success);
        Assert.True(toolWindows.IsVisible(explorer));
        Assert.True(toolWindows.Show(output).Success);
        Assert.True(toolWindows.IsVisible(output));

        Assert.Equal(
            "lattice.toolwindow.unknown",
            toolWindows.Show(new ToolWindowId("app.missing")).ErrorCode);
    }

    [Fact]
    public void SettingsUseDefaultsAndValidateValues()
    {
        var catalog = Build(builder =>
        {
            builder.AddSetting(new SettingDescriptor(
                new SettingsKey("app.timeout"),
                TestDescriptors.Key("timeout"),
                LatticeSettingKind.Integer,
                30,
                Min: 1,
                Max: 60));
            builder.AddSetting(new SettingDescriptor(
                new SettingsKey("app.mode"),
                TestDescriptors.Key("mode"),
                LatticeSettingKind.Select,
                "fast",
                [
                    new SettingChoice("fast", TestDescriptors.Key("fast")),
                    new SettingChoice("slow", TestDescriptors.Key("slow"))
                ]));
            builder.AddSetting(new SettingDescriptor(
                new SettingsKey("app.auto"),
                TestDescriptors.Key("auto"),
                LatticeSettingKind.Boolean,
                false));
        });
        var settings = new LatticeSettingsStore(catalog);
        var timeout = new SettingsKey("app.timeout");
        var changes = 0;
        settings.Changed += key =>
        {
            if (key == timeout)
            {
                changes++;
            }
        };

        Assert.Equal(30, settings.Get<int>(timeout));
        Assert.Equal("fast", settings.Get<string>(new SettingsKey("app.mode")));
        Assert.Null(settings.GetValue(new SettingsKey("app.missing")));

        Assert.True(settings.Set(timeout, 45).Success);
        Assert.Equal(45, settings.Get<int>(timeout));
        Assert.Equal(1, changes);
        Assert.Equal(45, settings.GetValue(timeout));

        Assert.Equal(
            "lattice.settings.invalid_value",
            settings.Set(timeout, 0).ErrorCode);
        Assert.Equal(
            "lattice.settings.invalid_value",
            settings.Set(new SettingsKey("app.mode"), "other").ErrorCode);
        Assert.Equal(
            "lattice.settings.invalid_value",
            settings.Set(new SettingsKey("app.auto"), "yes").ErrorCode);
        Assert.Equal(
            "lattice.settings.unknown_key",
            settings.Set(new SettingsKey("app.missing"), 1).ErrorCode);
    }

    [Fact]
    public void LocalizationResolvesCultureThenFallback()
    {
        var catalog = Build(builder =>
            builder.AddStrings(new LatticeStringCatalog("app", TestStrings.ResourceManager)));
        var localizer = new LatticeLocalizer(catalog, "ru");
        var changes = 0;
        localizer.Changed += () => changes++;

        Assert.Equal("Привет", localizer.Get(new StringKey("app", "title")));
        Assert.Equal("Fallback", localizer.Get(new StringKey("app", "only.en")));
        Assert.Equal("app.missing", localizer.Get(new StringKey("app", "missing")));

        localizer.SetCulture("en");
        Assert.Equal("Hello", localizer.Get(new StringKey("app", "title")));
        localizer.SetCulture("EN");
        Assert.Equal(1, changes);
    }

    [Fact]
    public void ErrorCodesResolveThroughCoreAndPluginResources()
    {
        var catalog = Build(builder =>
            builder.AddStrings(new LatticeStringCatalog("app", TestStrings.ResourceManager)));
        var localizer = new LatticeLocalizer(catalog, "ru");

        Assert.Equal(
            "Команда не зарегистрирована.",
            localizer.GetError("lattice.command.not_found"));
        Assert.Equal("Локализованная ошибка", localizer.GetError("app.failure"));
        Assert.Equal("fallback", localizer.GetError("app.unknown", "fallback"));
        Assert.Equal("app.unknown", localizer.GetError("app.unknown"));
    }

    [Fact]
    public void ShortcutsResolveOverridesAndReportConflicts()
    {
        var catalog = Build(builder =>
        {
            builder.AddCommand(
                TestDescriptors.Command("app.run") with { DefaultShortcut = "Ctrl+R" },
                TestDescriptors.NoOp);
            builder.AddCommand(
                TestDescriptors.Command("app.stop") with { DefaultShortcut = "Ctrl+S" },
                TestDescriptors.NoOp);
            builder.AddCommand(
                TestDescriptors.Command("app.other") with { DefaultShortcut = "Ctrl+R" },
                TestDescriptors.NoOp);
        });
        var shortcuts = new LatticeShortcutService(catalog);

        Assert.Equal(3, shortcuts.Bindings.Count);
        var conflict = Assert.Single(shortcuts.FindConflicts());
        Assert.Equal("Ctrl+R", conflict.Gesture);
        Assert.Equal(2, conflict.Commands.Count);

        var resolved = shortcuts.Resolve(" ctrl+r ");
        Assert.True(resolved.Success);
        Assert.Equal("app.run", resolved.Data.Value);

        Assert.True(shortcuts.ApplyOverrides(new Dictionary<string, string>
        {
            ["app.run"] = "Ctrl+Shift+R",
            ["app.stop"] = string.Empty
        }).Success);
        Assert.Equal(
            "Ctrl+Shift+R",
            shortcuts.Bindings.Single(binding => binding.Command.Value == "app.run").EffectiveShortcut);
        Assert.Null(
            shortcuts.Bindings.Single(binding => binding.Command.Value == "app.stop").EffectiveShortcut);
        Assert.Equal("app.other", shortcuts.Resolve("Ctrl+R").Data.Value);

        Assert.Equal(
            "lattice.shortcut.unknown_command",
            shortcuts.ApplyOverrides(new Dictionary<string, string>
            {
                ["app.missing"] = "Ctrl+M"
            }).ErrorCode);
    }

    [Fact]
    public void AddLatticeIsIdempotentAndRegistersTheScopedRuntime()
    {
        var configureCalls = 0;
        var services = new ServiceCollection();
        services.AddLattice(options => options.AddPlugin(new TestPlugin(
            "app.feature",
            builder =>
            {
                configureCalls++;
                builder.AddCommand(
                    TestDescriptors.Command("app.run"),
                    TestDescriptors.NoOp);
            })));
        services.AddLattice(options => options.AddPlugin(new TestPlugin("app.other")));

        Assert.Equal(1, configureCalls);
        Assert.Equal(
            1,
            services.Count(descriptor => descriptor.ServiceType == typeof(LatticePluginCatalog)));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var catalog = provider.GetRequiredService<LatticePluginCatalog>();
        var scoped = scope.ServiceProvider;

        Assert.Same(catalog, scoped.GetRequiredService<LatticePluginCatalog>());
        Assert.NotNull(scoped.GetRequiredService<ILatticeContext>());
        Assert.NotNull(scoped.GetRequiredService<ILatticeCommandService>());
        Assert.NotNull(scoped.GetRequiredService<ILatticeDocumentService>());
        Assert.NotNull(scoped.GetRequiredService<ILatticeToolWindowService>());
        Assert.NotNull(scoped.GetRequiredService<ISettingsStore>());
        Assert.NotNull(scoped.GetRequiredService<ILatticeLocalizer>());
        Assert.NotNull(scoped.GetRequiredService<ILatticeShortcutService>());
    }

    private static LatticePluginCatalog Build(Action<ILatticePluginBuilder> configure) =>
        LatticePluginCatalog.Build([new TestPlugin("app.feature", configure)]);
}
