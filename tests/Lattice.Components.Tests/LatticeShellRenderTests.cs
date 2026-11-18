using System.Net;
using Flare.Abstractions;
using Flare.Components.IDE;
using Flare.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
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

    private static ServiceProvider BuildProvider(params ILatticePlugin[] extraPlugins)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IFlareLocalization>(new TestLocalization());
        services.AddSingleton<Microsoft.JSInterop.IJSRuntime>(new ModuleJsRuntime());
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
                typeof(TestDocumentComponent)));
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
}
