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
        Assert.Contains("Settings", html, StringComparison.Ordinal);
        Assert.Contains("Timeout", html, StringComparison.Ordinal);
        Assert.Contains("30", html, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IFlareLocalization>(new TestLocalization());
        services.AddSingleton<Microsoft.JSInterop.IJSRuntime>(new ModuleJsRuntime());
        services.AddFlare();
        services.AddFlareIde();
        services.AddLattice(options => options
            .AddPlugin(new TestPlugin())
            .AddPlugin<LatticeHostPlugin>());
        services.AddLatticeComponents();
        return services.BuildServiceProvider();
    }

    private static async Task<string> RenderAsync(IServiceProvider provider)
    {
        provider.GetRequiredService<ILatticeLocalizer>().SetCulture("en");
        await using var renderer = new HtmlRenderer(
            provider,
            provider.GetRequiredService<ILoggerFactory>());
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(LatticeShell.StartContent)] =
                (RenderFragment)(content => content.AddContent(0, "Start content"))
        });
        return WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<LatticeShell>(parameters)).ToHtmlString()));
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
            builder.AddStrings("en", new Dictionary<string, string>
            {
                ["app.document"] = "Document",
                ["app.explorer"] = "Explorer",
                ["app.run"] = "Run",
                ["app.connection"] = "Connection",
                ["app.settings"] = "Settings",
                ["app.timeout"] = "Timeout"
            });
        }
    }

    public sealed class TestDocumentComponent : LatticeDocumentComponent
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "test-document");
            builder.AddContent(2, Context.Document.Title.ToString());
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
}
