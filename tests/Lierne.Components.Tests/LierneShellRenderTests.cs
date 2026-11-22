using System.Net;
using System.Text.Json;
using Flare.Abstractions;
using Flare.Components.IDE;
using Flare.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lierne.Components.Tests;

/// <summary>Tests that the Lierne shell renders the catalog through Flare.</summary>
public sealed class LierneShellRenderTests
{
    [Fact]
    public async Task RendersTheRibbonDocumentAndToolWindow()
    {
        await using var provider = BuildProvider();
        var documents = provider.GetRequiredService<ILierneDocumentService>();
        var opened = documents.Open(new DocumentTypeId("app.document"));
        Assert.True(opened.Success);

        var html = await RenderAsync(provider);

        Assert.Contains("Connection", html, StringComparison.Ordinal);
        Assert.Contains("Run", html, StringComparison.Ordinal);
        Assert.Contains("Document", html, StringComparison.Ordinal);
        Assert.Contains("test-document", html, StringComparison.Ordinal);
        Assert.Contains("app.document", html, StringComparison.Ordinal);
        Assert.Contains("Explorer", html, StringComparison.Ordinal);
        Assert.Contains("test-tool", html, StringComparison.Ordinal);
        Assert.Contains("Ready", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RendersTheStartContentWhenNoDocumentIsOpen()
    {
        await using var provider = BuildProvider();

        var html = await RenderAsync(provider);

        Assert.Contains("Start content", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpensTheOptionsDocumentAndRendersItsSettings()
    {
        await using var provider = BuildProvider();
        var commands = provider.GetRequiredService<ILierneCommandService>();

        var opened = await commands.ExecuteAsync(
            LierneHostPlugin.OpenOptionsCommand,
            cancellationToken: TestContext.Current.CancellationToken);
        var reopened = await commands.ExecuteAsync(
            LierneHostPlugin.OpenOptionsCommand,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(opened.Success);
        Assert.True(reopened.Success);
        var documents = provider.GetRequiredService<ILierneDocumentService>();
        Assert.Single(
            documents.Documents,
            document => document.TypeId == LierneHostPlugin.OptionsDocumentType);

        var html = await RenderAsync(provider);

        Assert.Contains("Tools", html, StringComparison.Ordinal);
        Assert.Contains("Options", html, StringComparison.Ordinal);
        Assert.Contains("General", html, StringComparison.Ordinal);
        Assert.Contains("Commands", html, StringComparison.Ordinal);
        Assert.Contains("Themes", html, StringComparison.Ordinal);
        Assert.Contains("Settings", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RendersAContributedStartPageInsteadOfTheHostFallback()
    {
        await using var provider = BuildProvider(new ShellPlugin());

        var html = await RenderAsync(provider);

        Assert.Contains("contributed-start", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Start content", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RendersAContributedToolbarInsteadOfTheRibbon()
    {
        await using var provider = BuildProvider(new ToolbarPlugin());

        var html = await RenderAsync(provider);

        Assert.Contains("test-toolbar", html, StringComparison.Ordinal);
        Assert.DoesNotContain("flare-ribbon", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeepsTheCenterDocumentWhenOpeningASideDocumentAndSelectsTheMovedTab()
    {
        await using var provider = BuildProvider();
        var documents = provider.GetRequiredService<ILierneDocumentService>();
        var type = new DocumentTypeId("app.document");
        documents.Open(type, payload: "center-body");
        await using var renderer = CreateRenderer(provider);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<LierneShell>(ShellParameters);
            var side = documents.Open(type, payload: "side-body", location: LierneDockLocation.Left).Data!;
            var both = root.ToHtmlString();
            Assert.Contains("center-body", both, StringComparison.Ordinal);
            Assert.Contains("side-body", both, StringComparison.Ordinal);

            Assert.True(documents.Move(side.Id, LierneDockLocation.Main).Success);
            var moved = root.ToHtmlString();
            Assert.Contains("side-body", moved, StringComparison.Ordinal);
            Assert.Contains("center-body", moved, StringComparison.Ordinal);
            Assert.Equal(side.Id, documents.Active!.Id);
        });
    }

    [Fact]
    public async Task RendersTheSameDocumentHeaderTemplateInEveryDockRegion()
    {
        await using var provider = BuildProvider();
        var documents = provider.GetRequiredService<ILierneDocumentService>();
        var type = new DocumentTypeId("app.document");
        var document = documents.Open(type, payload: "first").Data!;
        await using var renderer = CreateRenderer(provider);
        RenderFragment<LierneDocumentHeaderContext> header = context => builder =>
        {
            builder.OpenElement(0, "strong");
            builder.AddContent(1, $"{context.Title} | {context.Document.Payload}");
            builder.CloseElement();
        };
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(LierneShell.DocumentHeaderContent)] = header
        });

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<LierneShell>(parameters);
            Assert.Contains("<strong>Document | first</strong>", root.ToHtmlString(), StringComparison.Ordinal);
            Assert.True(documents.SetPayload(document.Id, "updated").Success);
            Assert.Contains("<strong>Document | updated</strong>", root.ToHtmlString(), StringComparison.Ordinal);

            Assert.True(documents.Move(document.Id, LierneDockLocation.Left).Success);
            Assert.Contains("<strong>Document | updated</strong>", root.ToHtmlString(), StringComparison.Ordinal);
            Assert.True(documents.SetPayload(document.Id, "side update").Success);
            Assert.Contains("<strong>Document | side update</strong>", root.ToHtmlString(), StringComparison.Ordinal);

            Assert.True(documents.Move(document.Id, LierneDockLocation.Right).Success);
            Assert.Contains("<strong>Document | side update</strong>", root.ToHtmlString(), StringComparison.Ordinal);
            Assert.Contains("aria-label=\"Document\"", root.ToHtmlString(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ShowsAndHidesABottomToolAlongsideTheHostContent()
    {
        var tool = new ToolWindowId("app.bottom");
        await using var provider = BuildProvider(new TestContribution("app.bottom", builder =>
            builder.AddToolWindow(new ToolWindowDescriptor(
                tool, StringKey.Literal("Bottom tool"), null, typeof(TestToolWindowComponent),
                LierneToolRegion.Bottom, DefaultVisible: false))));
        var tools = provider.GetRequiredService<ILierneToolWindowService>();
        await using var renderer = CreateRenderer(provider);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<LierneShell>(ShellParameters);
            Assert.DoesNotContain("Bottom tool", root.ToHtmlString(), StringComparison.Ordinal);

            Assert.True(tools.Show(tool).Success);
            var visible = root.ToHtmlString();
            Assert.Contains("Bottom tool", visible, StringComparison.Ordinal);
            Assert.Contains("host-bottom-content", visible, StringComparison.Ordinal);

            Assert.True(tools.Hide(tool).Success);
            var hidden = root.ToHtmlString();
            Assert.DoesNotContain("Bottom tool", hidden, StringComparison.Ordinal);
            Assert.Contains("host-bottom-content", hidden, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task UpdatesShortcutsForPluginsWithTheSameDisplayName()
    {
        var first = new CommandId("app.first.run");
        var second = new CommandId("app.second.run");
        await using var provider = BuildProvider(
            new TestContribution("app.first", builder => RegisterCommand(builder, first)),
            new TestContribution("app.second", builder => RegisterCommand(builder, second)));
        var shortcuts = provider.GetRequiredService<ILierneShortcutService>();
        await using var renderer = CreateRenderer(provider);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var page = await renderer.RenderComponentAsync<LierneCommandsSettingsPage>();
            Assert.True(shortcuts.ApplyOverrides(new Dictionary<string, string> { [second.Value] = "Ctrl+K" }).Success);
            Assert.Contains("Ctrl+K", WebUtility.HtmlDecode(page.ToHtmlString()), StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FlushSavesTheLatestWorkspaceAfterAnInFlightWrite(bool firstWriteFails)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var storage = new WorkspaceStorage(blockFirstWrite: true);
        await using var provider = BuildProvider(storage);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var documents = services.GetRequiredService<ILierneDocumentService>();
        var persistence = services.GetRequiredService<LierneStatePersistence>();
        await using var renderer = new InteractiveRenderer(services);

        string documentId = string.Empty;
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.StartAsync();
            var payload = new List<string> { "first" };
            documentId = documents.Open(new("app.document"), payload: payload).Data!.Id;
            try
            {
                await storage.FirstWriteStarted.Task.WaitAsync(cancellationToken);
                var before = renderer.Batches;
                for (var i = 0; i < 20; i++)
                {
                    payload[0] = $"edited {i}";
                    Assert.True(documents.SetPayload(documentId, payload).Success);
                }
                Assert.True(renderer.Batches > before);
                Assert.True(documents.SetTitle(documentId, StringKey.Literal("Final title")).Success);

                var flush = persistence.FlushAsync(cancellationToken);
                Assert.False(flush.IsCompleted);
                Assert.Equal(1, storage.WorkspaceWrites);
                if (firstWriteFails)
                {
                    storage.FirstWriteCompletion.SetException(new IOException("Temporary storage failure"));
                }
                else
                {
                    storage.FirstWriteCompletion.SetResult();
                }
                await flush;
            }
            finally
            {
                storage.FirstWriteCompletion.TrySetResult();
            }
        });

        Assert.Equal(2, storage.WorkspaceWrites);
        await using var restored = provider.CreateAsyncScope();
        await restored.ServiceProvider.GetRequiredService<LierneStatePersistence>().RestoreWorkspaceAsync(cancellationToken);
        var savedDocument = Assert.Single(restored.ServiceProvider.GetRequiredService<ILierneDocumentService>().Documents);
        Assert.Equal(documentId, savedDocument.Id);
        Assert.Equal(StringKey.Literal("Final title"), savedDocument.Title);
        Assert.Equal("edited 19", Assert.Single(Assert.IsType<List<string>>(savedDocument.Payload)));
    }

    [Fact]
    public async Task ATransientDocumentChangeRendersWithoutSavingTheWorkspace()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var storage = new WorkspaceStorage();
        await using var provider = BuildProvider(storage);
        var documents = provider.GetRequiredService<ILierneDocumentService>();
        var persistence = provider.GetRequiredService<LierneStatePersistence>();
        await using var renderer = new InteractiveRenderer(provider);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.StartAsync();
            var document = documents.Open(new("app.document")).Data!;
            await persistence.FlushAsync(cancellationToken);
            var before = renderer.Batches;
            Assert.True(documents.SetStatusIcon(document.Id, "connected", "#abcdef").Success);
            await persistence.FlushAsync(cancellationToken);

            Assert.True(renderer.Batches > before);
            Assert.Equal(1, storage.WorkspaceWrites);
        });
    }

    [Fact]
    public async Task SelectingATabSavesOnceAndReselectingItDoesNotSave()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var storage = new WorkspaceStorage();
        await using var provider = BuildProvider(storage);
        var documents = provider.GetRequiredService<ILierneDocumentService>();
        var persistence = provider.GetRequiredService<LierneStatePersistence>();
        await using var renderer = new InteractiveRenderer(provider);
        var first = documents.Open(new("app.document")).Data!;
        documents.Open(new("app.document"));

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.StartAsync();
            var workspace = renderer.GetWorkspace();
            var key = LierneDockLayoutAdapter.DocumentKey(first.Id);
            Assert.True(
                workspace.Items.Any(item => item.Key == key),
                $"Workspace items: {string.Join(", ", workspace.Items.Select(item => item.Key))}");
            var group = FindGroup(workspace.Layout, key);
            var proposed = ReplaceGroup(workspace.Layout, group with { ActiveKey = key });
            var change = new DockLayoutChange(
                DockAction.Activate, key, group.Key, workspace.Layout, proposed);

            await workspace.ApplyChangeAsync(change);
            await persistence.FlushAsync(cancellationToken);
            Assert.Equal(first.Id, documents.Active!.Id);
            Assert.Equal(1, storage.WorkspaceWrites);

            var repeat = new DockLayoutChange(
                DockAction.Activate, key, group.Key, proposed, proposed);
            await workspace.ApplyChangeAsync(repeat);
            await persistence.FlushAsync(cancellationToken);
            Assert.Equal(1, storage.WorkspaceWrites);
        });
    }

    private static DockGroup FindGroup(DockNode node, string key) =>
        FindGroupOrNull(node, key) ??
        throw new InvalidOperationException($"The dock layout has no group containing '{key}'.");

    private static DockGroup? FindGroupOrNull(DockNode node, string key) => node switch
    {
        DockGroup group when group.Items.Contains(key) => group,
        DockSplit split => FindGroupOrNull(split.First, key) ?? FindGroupOrNull(split.Second, key),
        _ => null
    };

    private static DockNode ReplaceGroup(DockNode node, DockGroup replacement) => node switch
    {
        DockGroup group when group.Key == replacement.Key => replacement,
        DockSplit split => split with
        {
            First = ReplaceGroup(split.First, replacement),
            Second = ReplaceGroup(split.Second, replacement)
        },
        _ => node
    };

    [Theory]
    [InlineData(true, typeof(string), "lattice.component.invalid_type")]
    [InlineData(false, typeof(TestStartComponent), "lattice.component.invalid_context")]
    public void RejectsInvalidDocumentComponentsDuringRegistration(bool componentsFirst, Type component, string code)
    {
        var services = new ServiceCollection();
        var plugin = new TestContribution("app.document", builder => builder.AddDocument(
            new DocumentDescriptor(new("app.document"), StringKey.Literal("Document"), null, component)));

        var exception = Assert.Throws<LierneCompositionException>(() =>
        {
            if (componentsFirst)
            {
                services.AddLierneComponents();
            }

            services.AddLierne(options => options.AddPlugin(plugin));
            services.AddLierneComponents();
        });

        Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Fact]
    public void ResolvesDocumentRegionsThroughNestedSplits()
    {
        var main = new DockGroup("group:main", ["doc:a"], "doc:a");
        var left = new DockGroup("group:left.docs", ["doc:b"], "doc:b");
        var root = new DockSplit("split:left", DockAxis.Horizontal, left, main, 0.3);
        var split = new DockSplit(
            "generated",
            DockAxis.Horizontal,
            new DockGroup("generated-group", ["doc:c"], "doc:c"),
            main,
            0.5);

        Assert.Equal(LierneDockLocation.Left, LierneDockLayoutAdapter.DocumentRegionOf(root, left.Key));
        Assert.Equal(LierneDockLocation.Main, LierneDockLayoutAdapter.DocumentRegionOf(root, main.Key));
        Assert.Equal(LierneDockLocation.Main, LierneDockLayoutAdapter.DocumentRegionOf(split, "generated-group"));
    }

    [Fact]
    public async Task HostCommandsOpenThePaletteAndPinTheActiveDocument()
    {
        await using var provider = BuildProvider();
        var documents = provider.GetRequiredService<ILierneDocumentService>();
        var commands = provider.GetRequiredService<ILierneCommandService>();
        var palette = provider.GetRequiredService<LiernePaletteService>();
        var document = documents.Open(new DocumentTypeId("app.document"), preview: true).Data!;

        var opened = await commands.ExecuteAsync(
            LierneHostPlugin.OpenPaletteCommand,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(opened.Success);
        Assert.True(palette.IsOpen);

        var pinned = await commands.ExecuteAsync(
            LierneHostPlugin.PinDocumentCommand,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(pinned.Success);
        var updated = Assert.Single(documents.Documents, item => item.Id == document.Id);
        Assert.True(updated.Pinned);
        Assert.False(updated.Preview);
    }

    [Fact]
    public async Task LimitsDockDropZonesWhenConfigured()
    {
        await using var provider = BuildProvider();
        provider.GetRequiredService<ILierneLocalizer>().SetCulture("en");
        await using var renderer = CreateRenderer(provider);
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(LierneWorkspace.DropZones)] = DockDropZones.Into
        });

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<LierneWorkspace>(parameters)).ToHtmlString());

        Assert.Contains("data-flare-dock-zone=\"into\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-flare-dock-zone=\"start\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-flare-dock-zone=\"top\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RendersTheSelectedDocumentViewAndSwitchesIt()
    {
        await using var provider = BuildProvider(new ViewPlugin());
        var documents = provider.GetRequiredService<ILierneDocumentService>();
        var document = documents.Open(ViewPlugin.Type).Data!;

        var summary = await RenderAsync(provider);

        Assert.Contains("Summary", summary, StringComparison.Ordinal);
        Assert.Contains("Details", summary, StringComparison.Ordinal);
        Assert.Contains("summary-body", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("details-body", summary, StringComparison.Ordinal);

        Assert.True(documents.SetSelectedView(document.Id, ViewPlugin.Details).Success);
        var details = await RenderAsync(provider);

        Assert.Contains("details-body", details, StringComparison.Ordinal);
        Assert.DoesNotContain("summary-body", details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MarksPreviewAndPinnedDocumentTabs()
    {
        await using var provider = BuildProvider();
        var documents = provider.GetRequiredService<ILierneDocumentService>();
        var document = documents.Open(new DocumentTypeId("app.document"), preview: true).Data!;

        var preview = await RenderAsync(provider);

        Assert.Contains("lierne-workspace__preview", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("lierne-workspace__pin", preview, StringComparison.Ordinal);

        Assert.True(documents.SetPreview(document.Id, false).Success);
        Assert.True(documents.SetPinned(document.Id, true).Success);
        var pinned = await RenderAsync(provider);

        Assert.DoesNotContain("lierne-workspace__preview", pinned, StringComparison.Ordinal);
        Assert.Contains("lierne-workspace__pin", pinned, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RendersCommandPlacementsWithTheirPresentation()
    {
        var plugin = new PlacementPlugin();
        await using var provider = BuildProvider(plugin);

        var html = WebUtility.HtmlDecode(await RenderAsync(provider));

        Assert.Contains("Placed Group", html, StringComparison.Ordinal);
        Assert.Contains("Placed Override", html, StringComparison.Ordinal);
        Assert.Contains("Placed Disabled", html, StringComparison.Ordinal);
        Assert.Contains("Status Run", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunsACommandFromThePalette()
    {
        var plugin = new PlacementPlugin();
        await using var provider = BuildProvider(plugin);
        provider.GetRequiredService<ILierneLocalizer>().SetCulture("en");
        var palette = provider.GetRequiredService<LiernePaletteService>();
        var presentations = provider.GetRequiredService<ICommandPresentationService>();
        palette.Open();
        await using var renderer = new InteractiveRenderer(provider);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.StartAsync();
            var descriptor = Assert.Single(presentations.Search("Placed Run"));
            await renderer.GetPalette().ExecuteAsync(descriptor);

            Assert.Equal(1, plugin.Executions);
            Assert.False(palette.IsOpen);
        });
    }

    [Fact]
    public async Task AppliesSavedPanelPlacementsToThePanelService()
    {
        var tool = new ToolWindowId("app.explorer");
        await using var provider = BuildProvider();
        await using var renderer = new InteractiveRenderer(provider);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.StartAsync();
            renderer.GetWorkspace().ApplyLayout(new LierneWorkspaceLayout
            {
                Panels =
                [
                    new PanelPlacement(tool, LierneToolRegion.Right, Order: 3)
                ]
            });
        });

        var panels = provider.GetRequiredService<IPanelService>();
        Assert.Equal(LierneToolRegion.Right, panels.Get(tool)!.Region);
        Assert.Equal(3, panels.Get(tool)!.Order);
    }

    private static void RegisterCommand(ILiernePluginBuilder builder, CommandId command) =>
        builder.AddCommand(new CommandDescriptor(command, StringKey.Literal(command.Value)),
            (_, _) => Task.FromResult(LierneCommandResult.Ok()));

    private static ServiceProvider BuildProvider(params ILiernePlugin[] extraPlugins) =>
        BuildProvider(null, extraPlugins);

    private static ServiceProvider BuildProvider(ILierneStateStorage? storage, params ILiernePlugin[] extraPlugins)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IFlareLocalization>(new TestLocalization());
        services.AddSingleton<Microsoft.JSInterop.IJSRuntime>(new ModuleJsRuntime());
        if (storage is not null)
        {
            services.AddSingleton(storage);
        }
        services.AddFlare();
        services.AddFlareIde();
        services.AddLierne(options =>
        {
            options.AddPlugin(new TestPlugin()).AddPlugin<LierneHostPlugin>();
            foreach (var plugin in extraPlugins)
            {
                options.AddPlugin(plugin);
            }
        });
        services.AddLierneComponents();
        return services.BuildServiceProvider();
    }

    private static async Task<string> RenderAsync(IServiceProvider provider)
    {
        await using var renderer = CreateRenderer(provider);
        return WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<LierneShell>(ShellParameters)).ToHtmlString()));
    }

    private static async Task<string> RenderWorkspaceAsync(
        IServiceProvider provider,
        Dictionary<string, object?> parameters)
    {
        provider.GetRequiredService<ILierneLocalizer>().SetCulture("en");
        await using var renderer = CreateRenderer(provider);
        var view = ParameterView.FromDictionary(parameters);
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<LierneWorkspace>(view)).ToHtmlString());
    }

    private static HtmlRenderer CreateRenderer(IServiceProvider provider)
    {
        provider.GetRequiredService<ILierneLocalizer>().SetCulture("en");
        return new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
    }

    private static ParameterView ShellParameters => ParameterView.FromDictionary(new Dictionary<string, object?>
    {
        [nameof(LierneShell.StartContent)] =
            (RenderFragment)(content => content.AddContent(0, "Start content")),
        [nameof(LierneShell.BottomPanel)] =
            (RenderFragment)(content => content.AddContent(0, "host-bottom-content"))
    });

    private sealed class TestContribution(string id, Action<ILiernePluginBuilder> configure) : ILiernePlugin
    {
        public PluginId Id { get; } = new(id);
        public StringKey DisplayName => StringKey.Literal("Shared display name");
        public IReadOnlyList<PluginId> DependsOn => [];
        public void Configure(ILiernePluginBuilder builder) => configure(builder);
    }

    private sealed class TestPlugin : ILiernePlugin
    {
        public PluginId Id => new("app.feature");

        public StringKey DisplayName => new("app", "feature");

        public IReadOnlyList<PluginId> DependsOn => [];

        public void Configure(ILiernePluginBuilder builder)
        {
            builder.AddDocument(new DocumentDescriptor(
                new DocumentTypeId("app.document"),
                new StringKey("app", "document"),
                null,
                typeof(TestDocumentComponent))
            {
                StateCodec = new LierneDocumentStateCodec(1,
                    payload => JsonSerializer.SerializeToElement(payload),
                    (_, payload) => payload.Deserialize<List<string>>())
            });
            builder.AddToolWindow(new ToolWindowDescriptor(
                new ToolWindowId("app.explorer"),
                new StringKey("app", "explorer"),
                null,
                typeof(TestToolWindowComponent)));
            builder.AddCommand(
                new CommandDescriptor(
                    new CommandId("app.run"),
                    new StringKey("app", "run")),
                (context, cancellationToken) => Task.FromResult(LierneCommandResult.Ok()));
            builder.AddRibbonGroup(new RibbonGroupDescriptor(
                new RibbonGroupId("app.connection"),
                new StringKey("app", "connection")));
            builder.AddRibbonItem(new RibbonItemDescriptor(
                new RibbonItemId("app.run.item"),
                new RibbonGroupId("app.connection"),
                new CommandId("app.run")));
            builder.AddSettingsPage(new SettingsPageDescriptor(
                new SettingsPageId("app.settings"),
                new StringKey("app", "settings"),
                0));
            builder.AddSetting(new SettingDescriptor(
                new SettingsKey("app.timeout"),
                new StringKey("app", "timeout"),
                LierneSettingKind.Integer,
                30,
                Page: new SettingsPageId("app.settings")));
            builder.AddStrings(new LierneStringCatalog("app", TestStrings.ResourceManager));
            builder.AddStatusBarItem(new StatusBarItemDescriptor(
                new StatusBarItemId("app.status"),
                new StringKey("app", "status")));
        }
    }

    public sealed class TestDocumentComponent : LierneDocumentComponent
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "test-document");
            builder.AddContent(2, Context.Document.Title.ToString());
            builder.AddContent(3, Context.Document.Payload?.ToString());
            builder.CloseElement();
        }
    }

    private sealed class ViewPlugin : ILiernePlugin
    {
        public static readonly DocumentTypeId Type = new("app.viewDocument");
        public static readonly DocumentViewId Summary = new("app.summary");
        public static readonly DocumentViewId Details = new("app.details");

        public PluginId Id => new("app.views");

        public StringKey DisplayName => StringKey.Literal("Views");

        public IReadOnlyList<PluginId> DependsOn => [];

        public void Configure(ILiernePluginBuilder builder)
        {
            builder.AddDocument(new DocumentDescriptor(
                Type,
                StringKey.Literal("View document"),
                null,
                typeof(TestDocumentComponent)));
            builder.AddDocumentView(new DocumentViewDescriptor(
                Type,
                Summary,
                StringKey.Literal("Summary"),
                null,
                typeof(SummaryViewComponent)));
            builder.AddDocumentView(new DocumentViewDescriptor(
                Type,
                Details,
                StringKey.Literal("Details"),
                null,
                typeof(DetailsViewComponent),
                Order: 10));
        }
    }

    public sealed class SummaryViewComponent : LierneDocumentViewComponent
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder) =>
            builder.AddContent(0, "summary-body");
    }

    public sealed class DetailsViewComponent : LierneDocumentViewComponent
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder) =>
            builder.AddContent(0, "details-body");
    }

    public sealed class TestToolWindowComponent : LierneToolWindowComponent
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "test-tool");
            builder.AddContent(2, "Tool body");
            builder.CloseElement();
        }
    }

    public sealed class TestToolbarComponent : LierneToolbarComponent
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "test-toolbar");
            builder.AddContent(2, "Toolbar content");
            builder.CloseElement();
        }
    }

    public sealed class TestStartComponent : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "contributed-start");
            builder.AddContent(2, "Contributed start");
            builder.CloseElement();
        }
    }

    private sealed class ShellPlugin : ILiernePlugin
    {
        public PluginId Id => new("app.shell");

        public StringKey DisplayName => new("app", "shell");

        public IReadOnlyList<PluginId> DependsOn => [];

        public void Configure(ILiernePluginBuilder builder) =>
            builder.AddShellContent(new ShellContentDescriptor(
                new ShellContentId("app.start"),
                LierneShellSlot.StartPage,
                typeof(TestStartComponent)));
    }

    private sealed class PlacementPlugin : ILiernePlugin
    {
        private static readonly CommandId Run = new("app.placement.run");
        private static readonly CommandId Disabled = new("app.placement.disabled");
        private static readonly RibbonGroupId Group = new("app.placement.group");

        public int Executions { get; private set; }

        public PluginId Id => new("app.placement");

        public StringKey DisplayName => StringKey.Literal("Placement");

        public IReadOnlyList<PluginId> DependsOn => [];

        public void Configure(ILiernePluginBuilder builder)
        {
            builder.AddCommand(
                new CommandDescriptor(Run, StringKey.Literal("Placed Run")),
                (_, _) =>
                {
                    Executions++;
                    return Task.FromResult(LierneCommandResult.Ok());
                });
            builder.AddCommand(
                new CommandDescriptor(Disabled, StringKey.Literal("Placed Disabled")),
                (_, _) => Task.FromResult(LierneCommandResult.Ok()),
                _ => false);
            builder.AddRibbonGroup(new RibbonGroupDescriptor(Group, StringKey.Literal("Placed Group")));
            builder.AddCommandPlacement(new CommandPlacementDescriptor(
                Run,
                CommandSurface.Ribbon,
                Group.Value,
                10,
                TitleOverride: StringKey.Literal("Placed Override")));
            builder.AddCommandPlacement(new CommandPlacementDescriptor(
                Disabled,
                CommandSurface.Ribbon,
                Group.Value,
                20));
            builder.AddCommandPlacement(new CommandPlacementDescriptor(
                Run,
                CommandSurface.StatusBar,
                "right",
                0,
                TitleOverride: StringKey.Literal("Status Run")));
        }
    }

    private sealed class ToolbarPlugin : ILiernePlugin
    {
        public PluginId Id => new("app.toolbar");

        public StringKey DisplayName => new("app", "toolbar");

        public IReadOnlyList<PluginId> DependsOn => [];

        public void Configure(ILiernePluginBuilder builder) =>
            builder.AddToolbar(new ToolbarDescriptor(
                new ToolbarId("app.toolbar"),
                typeof(TestToolbarComponent)));
    }

#pragma warning disable BL0006 // Test the interactive rendering lifecycle without a browser.
    private sealed class InteractiveRenderer(IServiceProvider services)
        : Renderer(services, services.GetRequiredService<ILoggerFactory>())
    {
        private int _shellId;

        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        public int Batches { get; private set; }

        public Task StartAsync()
        {
            _shellId = AssignRootComponentId(InstantiateComponent(typeof(LierneShell)));
            return RenderRootComponentAsync(_shellId, ShellParameters);
        }

        public LierneWorkspace GetWorkspace() => Find<LierneWorkspace>("workspace");

        public LierneCommandPalette GetPalette() => Find<LierneCommandPalette>("command palette");

        private T Find<T>(string name) where T : class, IComponent
        {
            var frames = GetCurrentRenderTreeFrames(_shellId);
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames.Array[index];
                if (frame.FrameType == RenderTreeFrameType.Component && frame.Component is T component)
                {
                    return component;
                }
            }

            throw new InvalidOperationException($"The shell did not render a {name}.");
        }

        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
        {
            Batches++;
            return Task.CompletedTask;
        }

        protected override void HandleException(Exception exception) =>
            throw new InvalidOperationException("The shell failed to render.", exception);
    }
#pragma warning restore BL0006

    private sealed class WorkspaceStorage(bool blockFirstWrite = false) : ILierneStateStorage
    {
        private string? _workspace;
        public int WorkspaceWrites { get; private set; }
        public TaskCompletionSource FirstWriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FirstWriteCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<string?> ReadAsync(LierneStateSection section, CancellationToken cancellationToken) =>
            ValueTask.FromResult(section == LierneStateSection.Workspace ? _workspace : null);

        public async ValueTask WriteAsync(LierneStateSection section, string json, CancellationToken cancellationToken)
        {
            if (section == LierneStateSection.Workspace)
            {
                if (++WorkspaceWrites == 1 && blockFirstWrite)
                {
                    FirstWriteStarted.SetResult();
                    await FirstWriteCompletion.Task.WaitAsync(cancellationToken);
                }
                _workspace = json;
            }
        }
    }
}
