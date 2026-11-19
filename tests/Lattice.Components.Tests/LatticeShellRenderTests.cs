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

namespace Lattice.Components.Tests;

/// <summary>Tests that the Lattice shell renders the catalog through Flare.</summary>
public sealed class LatticeShellRenderTests
{
    [Fact]
    public async Task RendersTheRibbonDocumentAndToolWindow()
    {
        await using var provider = BuildProvider();
        var documents = provider.GetRequiredService<ILatticeDocumentService>();
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
        var commands = provider.GetRequiredService<ILatticeCommandService>();

        var opened = await commands.ExecuteAsync(
            LatticeHostPlugin.OpenOptionsCommand,
            cancellationToken: TestContext.Current.CancellationToken);
        var reopened = await commands.ExecuteAsync(
            LatticeHostPlugin.OpenOptionsCommand,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(opened.Success);
        Assert.True(reopened.Success);
        var documents = provider.GetRequiredService<ILatticeDocumentService>();
        Assert.Single(
            documents.Documents,
            document => document.TypeId == LatticeHostPlugin.OptionsDocumentType);

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
        var documents = provider.GetRequiredService<ILatticeDocumentService>();
        var type = new DocumentTypeId("app.document");
        documents.Open(type, payload: "center-body");
        await using var renderer = CreateRenderer(provider);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<LatticeShell>(ShellParameters);
            var side = documents.Open(type, payload: "side-body", location: LatticeDockLocation.Left).Data!;
            var both = root.ToHtmlString();
            Assert.Contains("center-body", both, StringComparison.Ordinal);
            Assert.Contains("side-body", both, StringComparison.Ordinal);

            Assert.True(documents.Move(side.Id, LatticeDockLocation.Main).Success);
            var moved = root.ToHtmlString();
            Assert.Matches("""data-region="Center"><span class="test-document">[^<]*side-body</span>""", moved);
            Assert.Contains($"data-flare-doc-key=\"{side.Id}\"", moved, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task RendersTheSameDocumentHeaderTemplateInEveryDockRegion()
    {
        await using var provider = BuildProvider();
        var documents = provider.GetRequiredService<ILatticeDocumentService>();
        var type = new DocumentTypeId("app.document");
        var document = documents.Open(type, payload: "first").Data!;
        await using var renderer = CreateRenderer(provider);
        RenderFragment<LatticeDocumentHeaderContext> header = context => builder =>
        {
            builder.OpenElement(0, "strong");
            builder.AddContent(1, $"{context.Title} | {context.Document.Payload}");
            builder.CloseElement();
        };
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(LatticeShell.DocumentHeaderContent)] = header
        });

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            if (typeof(FlareDocumentTab).GetProperty("HeaderContent") is null ||
                typeof(FlareIdeLayout).GetProperty("DocumentHeaderContent") is null)
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await renderer.RenderComponentAsync<LatticeShell>(parameters));
                Assert.Contains("Flare build with document tab header templates", error.Message, StringComparison.Ordinal);
                return;
            }

            var root = await renderer.RenderComponentAsync<LatticeShell>(parameters);
            Assert.Contains("<strong>Document | first</strong>", root.ToHtmlString(), StringComparison.Ordinal);
            Assert.True(documents.SetPayload(document.Id, "updated").Success);
            Assert.Contains("<strong>Document | updated</strong>", root.ToHtmlString(), StringComparison.Ordinal);

            Assert.True(documents.Move(document.Id, LatticeDockLocation.Left).Success);
            Assert.Contains("<strong>Document | updated</strong>", root.ToHtmlString(), StringComparison.Ordinal);
            Assert.True(documents.SetPayload(document.Id, "side update").Success);
            Assert.Contains("<strong>Document | side update</strong>", root.ToHtmlString(), StringComparison.Ordinal);

            Assert.True(documents.Move(document.Id, LatticeDockLocation.Right).Success);
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
                LatticeToolRegion.Bottom, DefaultVisible: false))));
        var tools = provider.GetRequiredService<ILatticeToolWindowService>();
        await using var renderer = CreateRenderer(provider);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<LatticeShell>(ShellParameters);
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
        var shortcuts = provider.GetRequiredService<ILatticeShortcutService>();
        await using var renderer = CreateRenderer(provider);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var page = await renderer.RenderComponentAsync<LatticeCommandsSettingsPage>();
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
        var documents = services.GetRequiredService<ILatticeDocumentService>();
        var persistence = services.GetRequiredService<LatticeStatePersistence>();
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
        await restored.ServiceProvider.GetRequiredService<LatticeStatePersistence>().RestoreWorkspaceAsync(cancellationToken);
        var savedDocument = Assert.Single(restored.ServiceProvider.GetRequiredService<ILatticeDocumentService>().Documents);
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
        var documents = provider.GetRequiredService<ILatticeDocumentService>();
        var persistence = provider.GetRequiredService<LatticeStatePersistence>();
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
        var documents = provider.GetRequiredService<ILatticeDocumentService>();
        var persistence = provider.GetRequiredService<LatticeStatePersistence>();
        await using var renderer = new InteractiveRenderer(provider);
        var first = documents.Open(new("app.document")).Data!;
        documents.Open(new("app.document"));

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.StartAsync();
            var select = renderer.GetLayout().OnActiveDocumentChanged;
            var selection = new IdeDocumentSelection(IdeDocumentRegion.Center, first.Id);

            await select.InvokeAsync(selection);
            await persistence.FlushAsync(cancellationToken);
            Assert.Equal(first.Id, documents.Active!.Id);
            Assert.Equal(1, storage.WorkspaceWrites);

            await select.InvokeAsync(selection);
            await persistence.FlushAsync(cancellationToken);
            Assert.Equal(1, storage.WorkspaceWrites);
        });
    }

    [Theory]
    [InlineData(true, typeof(string), "lattice.component.invalid_type")]
    [InlineData(false, typeof(TestStartComponent), "lattice.component.invalid_context")]
    public void RejectsInvalidDocumentComponentsDuringRegistration(bool componentsFirst, Type component, string code)
    {
        var services = new ServiceCollection();
        var plugin = new TestContribution("app.document", builder => builder.AddDocument(
            new DocumentDescriptor(new("app.document"), StringKey.Literal("Document"), null, component)));

        var exception = Assert.Throws<LatticeCompositionException>(() =>
        {
            if (componentsFirst)
            {
                services.AddLatticeComponents();
            }

            services.AddLattice(options => options.AddPlugin(plugin));
            services.AddLatticeComponents();
        });

        Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    private static void RegisterCommand(ILatticePluginBuilder builder, CommandId command) =>
        builder.AddCommand(new CommandDescriptor(command, StringKey.Literal(command.Value)),
            (_, _) => Task.FromResult(LatticeCommandResult.Ok()));

    private static ServiceProvider BuildProvider(params ILatticePlugin[] extraPlugins) =>
        BuildProvider(null, extraPlugins);

    private static ServiceProvider BuildProvider(ILatticeStateStorage? storage, params ILatticePlugin[] extraPlugins)
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
        services.AddLattice(options =>
        {
            options.AddPlugin(new TestPlugin()).AddPlugin<LatticeHostPlugin>();
            foreach (var plugin in extraPlugins)
            {
                options.AddPlugin(plugin);
            }
        });
        services.AddLatticeComponents();
        return services.BuildServiceProvider();
    }

    private static async Task<string> RenderAsync(IServiceProvider provider)
    {
        await using var renderer = CreateRenderer(provider);
        return WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<LatticeShell>(ShellParameters)).ToHtmlString()));
    }

    private static HtmlRenderer CreateRenderer(IServiceProvider provider)
    {
        provider.GetRequiredService<ILatticeLocalizer>().SetCulture("en");
        return new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
    }

    private static ParameterView ShellParameters => ParameterView.FromDictionary(new Dictionary<string, object?>
    {
        [nameof(LatticeShell.StartContent)] =
            (RenderFragment)(content => content.AddContent(0, "Start content")),
        [nameof(LatticeShell.BottomPanel)] =
            (RenderFragment)(content => content.AddContent(0, "host-bottom-content"))
    });

    private sealed class TestContribution(string id, Action<ILatticePluginBuilder> configure) : ILatticePlugin
    {
        public PluginId Id { get; } = new(id);
        public StringKey DisplayName => StringKey.Literal("Shared display name");
        public IReadOnlyList<PluginId> DependsOn => [];
        public void Configure(ILatticePluginBuilder builder) => configure(builder);
    }

    private sealed class TestPlugin : ILatticePlugin
    {
        public PluginId Id => new("app.feature");

        public StringKey DisplayName => new("app", "feature");

        public IReadOnlyList<PluginId> DependsOn => [];

        public void Configure(ILatticePluginBuilder builder)
        {
            builder.AddDocument(new DocumentDescriptor(
                new DocumentTypeId("app.document"),
                new StringKey("app", "document"),
                null,
                typeof(TestDocumentComponent))
            {
                StateCodec = new LatticeDocumentStateCodec(1,
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
                (context, cancellationToken) => Task.FromResult(LatticeCommandResult.Ok()));
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
                LatticeSettingKind.Integer,
                30,
                Page: new SettingsPageId("app.settings")));
            builder.AddStrings(new LatticeStringCatalog("app", TestStrings.ResourceManager));
            builder.AddStatusBarItem(new StatusBarItemDescriptor(
                new StatusBarItemId("app.status"),
                new StringKey("app", "status")));
        }
    }

    public sealed class TestDocumentComponent : LatticeDocumentComponent
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

    public sealed class TestToolWindowComponent : LatticeToolWindowComponent
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "test-tool");
            builder.AddContent(2, "Tool body");
            builder.CloseElement();
        }
    }

    public sealed class TestToolbarComponent : LatticeToolbarComponent
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

    private sealed class ShellPlugin : ILatticePlugin
    {
        public PluginId Id => new("app.shell");

        public StringKey DisplayName => new("app", "shell");

        public IReadOnlyList<PluginId> DependsOn => [];

        public void Configure(ILatticePluginBuilder builder) =>
            builder.AddShellContent(new ShellContentDescriptor(
                new ShellContentId("app.start"),
                LatticeShellSlot.StartPage,
                typeof(TestStartComponent)));
    }

    private sealed class ToolbarPlugin : ILatticePlugin
    {
        public PluginId Id => new("app.toolbar");

        public StringKey DisplayName => new("app", "toolbar");

        public IReadOnlyList<PluginId> DependsOn => [];

        public void Configure(ILatticePluginBuilder builder) =>
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
            _shellId = AssignRootComponentId(InstantiateComponent(typeof(LatticeShell)));
            return RenderRootComponentAsync(_shellId, ShellParameters);
        }

        public FlareIdeLayout GetLayout()
        {
            var frames = GetCurrentRenderTreeFrames(_shellId);
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames.Array[index];
                if (frame.FrameType == RenderTreeFrameType.Component && frame.Component is FlareIdeLayout layout)
                {
                    return layout;
                }
            }

            throw new InvalidOperationException("The shell did not render an IDE layout.");
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

    private sealed class WorkspaceStorage(bool blockFirstWrite = false) : ILatticeStateStorage
    {
        private string? _workspace;
        public int WorkspaceWrites { get; private set; }
        public TaskCompletionSource FirstWriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FirstWriteCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<string?> ReadAsync(LatticeStateSection section, CancellationToken cancellationToken) =>
            ValueTask.FromResult(section == LatticeStateSection.Workspace ? _workspace : null);

        public async ValueTask WriteAsync(LatticeStateSection section, string json, CancellationToken cancellationToken)
        {
            if (section == LatticeStateSection.Workspace)
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
