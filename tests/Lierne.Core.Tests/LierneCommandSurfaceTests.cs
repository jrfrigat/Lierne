using Microsoft.Extensions.DependencyInjection;

namespace Lierne.Core.Tests;

/// <summary>Tests command presentations, placements, search and keymap candidates.</summary>
public sealed class LierneCommandSurfaceTests
{
    private static readonly CommandId Run = new("tests.run");
    private static readonly CommandId Close = new("tests.close");

    [Fact]
    public void PresentationUsesDescriptorAndPredicate()
    {
        using var provider = BuildProvider(builder =>
        {
            builder.AddCommand(
                new CommandDescriptor(
                    Run,
                    TestDescriptors.Key("run"),
                    "play",
                    "Ctrl+R",
                    Description: TestDescriptors.Key("runDescription"),
                    Keywords: ["execute"]),
                TestDescriptors.NoOp,
                _ => false);
        });
        using var scope = provider.CreateScope();
        var presentations = scope.ServiceProvider.GetRequiredService<ICommandPresentationService>();

        var presentation = presentations.GetPresentation(Run);

        Assert.False(presentation.Enabled);
        Assert.True(presentation.Visible);
        Assert.Equal(TestDescriptors.Key("run"), presentation.Title);
        Assert.Equal("play", presentation.IconKey);
        Assert.Equal("Ctrl+R", presentation.ShortcutText);
    }

    [Fact]
    public async Task UpdateHandlerComputesAndCachesPresentation()
    {
        using var provider = BuildProvider(builder =>
        {
            builder.AddCommand(new CommandDescriptor(Run, TestDescriptors.Key("run")), TestDescriptors.NoOp);
            builder.AddCommandUpdateHandler(Run, new TestUpdateHandler(Run));
        });
        using var scope = provider.CreateScope();
        var presentations = scope.ServiceProvider.GetRequiredService<ICommandPresentationService>();
        var changed = new List<CommandId>();
        presentations.Changed += changed.Add;

        var presentation = await presentations.UpdateAsync(
            Run, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(TestDescriptors.Key("updated"), presentation.Title);
        Assert.True(presentation.Checked);
        Assert.Equal("Ctrl+U", presentation.ShortcutText);
        Assert.Contains(Run, changed);
        Assert.Equal(presentation, presentations.GetPresentation(Run));
    }

    [Fact]
    public async Task OlderAsyncUpdateCannotOverwriteTheLatestPresentation()
    {
        var pending = new TaskCompletionSource<CommandPresentation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var updated = new CommandPresentation(Run, TestDescriptors.Key("new"));
        using var provider = BuildProvider(builder =>
        {
            builder.AddCommand(new(Run, TestDescriptors.Key("run"), ArgumentType: typeof(bool)), TestDescriptors.NoOp);
            builder.AddCommandUpdateHandler(Run, new OrderedUpdateHandler(pending.Task, updated));
        });
        using var scope = provider.CreateScope();
        var presentations = scope.ServiceProvider.GetRequiredService<ICommandPresentationService>();
        var cancellationToken = TestContext.Current.CancellationToken;
        var older = presentations.UpdateAsync(Run, false, cancellationToken);
        Assert.Equal(updated, await presentations.UpdateAsync(Run, true, cancellationToken));
        pending.SetResult(new CommandPresentation(Run, TestDescriptors.Key("old")));
        await older;

        Assert.Equal(updated, presentations.GetPresentation(Run));
    }

    [Fact]
    public void PlacementsAreOrderedInsideSurface()
    {
        using var provider = BuildProvider(builder =>
        {
            builder.AddCommand(new CommandDescriptor(Run, TestDescriptors.Key("run")), TestDescriptors.NoOp);
            builder.AddCommand(new CommandDescriptor(Close, TestDescriptors.Key("close")), TestDescriptors.NoOp);
            builder.AddCommandPlacement(new CommandPlacementDescriptor(
                Run, CommandSurface.Menu, "tests.menu", Order: 20));
            builder.AddCommandPlacement(new CommandPlacementDescriptor(
                Close, CommandSurface.Menu, "tests.menu", Order: 10));
            builder.AddCommandPlacement(new CommandPlacementDescriptor(
                Run, CommandSurface.Ribbon, "tests.ribbon"));
        });
        using var scope = provider.CreateScope();
        var placements = scope.ServiceProvider.GetRequiredService<ICommandPlacementService>();

        Assert.Equal(3, placements.All.Count);
        Assert.Equal(new[] { Close, Run }, placements.ForSurface(CommandSurface.Menu).Select(item => item.Command));
        Assert.Single(placements.ForSurface(CommandSurface.Ribbon));
        Assert.Empty(placements.ForSurface(CommandSurface.ContextMenu));
    }

    [Fact]
    public void SearchMatchesTitleDescriptionAndKeywords()
    {
        using var provider = BuildProvider(builder =>
        {
            builder.AddCommand(
                new CommandDescriptor(
                    Run,
                    TestDescriptors.Key("runQuery"),
                    Description: TestDescriptors.Key("runQueryDescription"),
                    Keywords: ["execute", "sql"]),
                TestDescriptors.NoOp);
            builder.AddCommand(
                new CommandDescriptor(Close, TestDescriptors.Key("close")),
                TestDescriptors.NoOp);
        });
        using var scope = provider.CreateScope();
        var presentations = scope.ServiceProvider.GetRequiredService<ICommandPresentationService>();

        Assert.Equal(new[] { Run }, presentations.Search("execute").Select(command => command.Id));
        Assert.Equal(new[] { Run }, presentations.Search("runq").Select(command => command.Id));
        Assert.Equal(new[] { Run, Close }, presentations.Search(string.Empty).Select(command => command.Id));
        Assert.Empty(presentations.Search("missing"));
    }

    [Fact]
    public void ShortcutServiceReturnsEveryCandidate()
    {
        using var provider = BuildProvider(builder =>
        {
            builder.AddCommand(
                new CommandDescriptor(Run, TestDescriptors.Key("run"), DefaultShortcut: "Ctrl+K"),
                TestDescriptors.NoOp);
            builder.AddCommand(
                new CommandDescriptor(Close, TestDescriptors.Key("close"), DefaultShortcut: "Ctrl+K"),
                TestDescriptors.NoOp);
        });
        using var scope = provider.CreateScope();
        var shortcuts = scope.ServiceProvider.GetRequiredService<ILierneShortcutService>();

        Assert.Equal(new[] { Run, Close }, shortcuts.ResolveAll("ctrl+k"));
        var first = shortcuts.Resolve("CTRL+K");
        Assert.True(first.Success);
        Assert.Equal(Run, first.Data);
    }

    [Fact]
    public void UnknownPlacementsAndUpdateHandlersProduceDiagnostics()
    {
        var catalog = LiernePluginCatalog.Build(
        [
            new TestPlugin("tests.commands", builder =>
            {
                builder.AddCommandPlacement(new CommandPlacementDescriptor(
                    Run, CommandSurface.Menu, "tests.menu"));
                builder.AddCommandUpdateHandler(Run, new TestUpdateHandler(Run));
            })
        ]);

        Assert.Contains(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "lattice.commandplacement.unknown_command");
        Assert.Contains(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "lattice.commandupdate.unknown_command");
    }

    private static ServiceProvider BuildProvider(Action<ILiernePluginBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddLierne(options => options.AddPlugin(
            new TestPlugin("tests.commands", configure)));
        return services.BuildServiceProvider();
    }

    private sealed class TestUpdateHandler(CommandId command) : ICommandUpdateHandler
    {
        public ValueTask<CommandPresentation> UpdateAsync(
            LierneCommandContext context,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new CommandPresentation(
                command,
                TestDescriptors.Key("updated"),
                Checked: true,
                ShortcutText: "Ctrl+U"));
    }

    private sealed class OrderedUpdateHandler(Task<CommandPresentation> pending, CommandPresentation updated)
        : ICommandUpdateHandler
    {
        public ValueTask<CommandPresentation> UpdateAsync(
            LierneCommandContext context,
            CancellationToken cancellationToken = default) =>
            context.Argument is true ? ValueTask.FromResult(updated) : new(pending);
    }
}
