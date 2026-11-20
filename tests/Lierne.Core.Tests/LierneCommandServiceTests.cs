using Microsoft.Extensions.DependencyInjection;

namespace Lierne.Core.Tests;

/// <summary>Tests command execution, argument checks, predicates and observers.</summary>
public sealed class LierneCommandServiceTests
{
    [Fact]
    public async Task ExecutesTheHandlerAndReturnsItsData()
    {
        var catalog = Build(builder => builder.AddCommand(
            TestDescriptors.Command("app.run"),
            (context, cancellationToken) =>
                Task.FromResult(LierneCommandResult.Ok(42))));
        using var provider = new ServiceCollection().BuildServiceProvider();
        var service = new LierneCommandService(catalog, new LierneContext(), provider);

        var result = await service.ExecuteAsync(
            new CommandId("app.run"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(42, result.Data);
    }

    [Fact]
    public async Task RejectsAnArgumentOfTheWrongTypeWithoutRunningTheHandler()
    {
        var executed = false;
        var catalog = Build(builder => builder.AddCommand(
            TestDescriptors.Command("app.run", typeof(int)),
            (context, cancellationToken) =>
            {
                executed = true;
                return Task.FromResult(LierneCommandResult.Ok());
            }));
        using var provider = new ServiceCollection().BuildServiceProvider();
        var service = new LierneCommandService(catalog, new LierneContext(), provider);

        var result = await service.ExecuteAsync(
            new CommandId("app.run"),
            "1",
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("lattice.command.invalid_argument", result.ErrorCode);
        Assert.False(executed);
    }

    [Fact]
    public async Task RejectsAnArgumentWhenTheCommandDeclaresNone()
    {
        var catalog = Build(builder => builder.AddCommand(
            TestDescriptors.Command("app.run"),
            TestDescriptors.NoOp));
        using var provider = new ServiceCollection().BuildServiceProvider();
        var service = new LierneCommandService(catalog, new LierneContext(), provider);

        var result = await service.ExecuteAsync(
            new CommandId("app.run"),
            1,
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("lattice.command.invalid_argument", result.ErrorCode);
    }

    [Fact]
    public async Task ChecksTheCanExecutePredicateAgainstTheSharedState()
    {
        var catalog = Build(builder => builder.AddCommand(
            TestDescriptors.Command("app.run"),
            TestDescriptors.NoOp,
            state => state.Get<bool>("enabled")));
        using var provider = new ServiceCollection().BuildServiceProvider();
        var state = new LierneContext();
        var service = new LierneCommandService(catalog, state, provider);

        var disabled = await service.ExecuteAsync(
            new CommandId("app.run"),
            cancellationToken: TestContext.Current.CancellationToken);
        state.Set("enabled", true);
        var enabled = await service.ExecuteAsync(
            new CommandId("app.run"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("lattice.command.disabled", disabled.ErrorCode);
        Assert.True(enabled.Success);
    }

    [Fact]
    public async Task ConvertsHandlerExceptionsToAFailedResult()
    {
        var catalog = Build(builder => builder.AddCommand(
            TestDescriptors.Command("app.run"),
            (context, cancellationToken) =>
                throw new InvalidOperationException("boom")));
        using var provider = new ServiceCollection().BuildServiceProvider();
        var service = new LierneCommandService(catalog, new LierneContext(), provider);

        var result = await service.ExecuteAsync(
            new CommandId("app.run"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("lattice.command.failed", result.ErrorCode);
        Assert.Equal("boom", result.ErrorMessage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NotifiesObserversAroundTheHandlerInRegistrationOrder(bool observerFails)
    {
        var log = new List<string>();
        var catalog = Build(builder => builder.AddCommand(
            TestDescriptors.Command("app.run"),
            (context, cancellationToken) =>
            {
                log.Add("run");
                return Task.FromResult(LierneCommandResult.Ok());
            }));
        var services = new ServiceCollection();
        services.AddSingleton<ILierneCommandObserver>(new RecordingObserver(log, "first"));
        if (observerFails)
        {
            services.AddSingleton<ILierneCommandObserver>(new ThrowingObserver());
        }

        services.AddSingleton<ILierneCommandObserver>(new RecordingObserver(log, "second"));
        using var provider = services.BuildServiceProvider();
        var service = new LierneCommandService(catalog, new LierneContext(), provider);

        var result = await service.ExecuteAsync(
            new CommandId("app.run"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(
            ["first.before", "second.before", "run", "first.after", "second.after"],
            log);
    }

    [Fact]
    public async Task ReportsUnknownCommands()
    {
        var catalog = Build(_ => { });
        using var provider = new ServiceCollection().BuildServiceProvider();
        var service = new LierneCommandService(catalog, new LierneContext(), provider);

        var result = await service.ExecuteAsync(
            new CommandId("app.missing"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("lattice.command.not_found", result.ErrorCode);
    }

    [Fact]
    public async Task LocalizesAKnownErrorCodeWhenTheHostProvidesALocalizer()
    {
        var catalog = Build(_ => { });
        using var provider = new ServiceCollection()
            .AddSingleton<ILierneLocalizer>(new LierneLocalizer(catalog, "en"))
            .BuildServiceProvider();
        var service = new LierneCommandService(catalog, new LierneContext(), provider);

        var result = await service.ExecuteAsync(
            new CommandId("app.missing"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("lattice.command.not_found", result.ErrorCode);
        Assert.Equal("The command is not registered.", result.ErrorMessage);
    }

    private static LiernePluginCatalog Build(Action<ILiernePluginBuilder> configure) =>
        LiernePluginCatalog.Build([new TestPlugin("app.feature", configure)]);

    private sealed class ThrowingObserver : ILierneCommandObserver
    {
        public ValueTask OnExecutingAsync(LierneCommandExecution execution) =>
            throw new InvalidOperationException("Observer failed before the command.");

        public ValueTask OnExecutedAsync(LierneCommandExecution execution, LierneCommandResult result) =>
            ValueTask.FromException(new InvalidOperationException("Observer failed after the command."));
    }

    private sealed class RecordingObserver(List<string> log, string name) : ILierneCommandObserver
    {
        public ValueTask OnExecutingAsync(LierneCommandExecution execution)
        {
            log.Add($"{name}.before");
            return ValueTask.CompletedTask;
        }

        public ValueTask OnExecutedAsync(
            LierneCommandExecution execution,
            LierneCommandResult result)
        {
            log.Add($"{name}.after");
            return ValueTask.CompletedTask;
        }
    }
}
