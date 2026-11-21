using Microsoft.Extensions.DependencyInjection;

namespace Lierne.Core.Tests;

/// <summary>Tests readiness, progress, events, disposal and state channels.</summary>
public sealed class LierneFoundationsTests
{
    [Fact]
    public async Task ReadinessWaitsForEveryPendingToken()
    {
        var readiness = new LierneReadinessService();
        var scope = new LierneReadinessScope("tests.workspace");
        var changes = new List<LierneReadinessChange>();
        readiness.Changed += changes.Add;

        Assert.True(readiness.IsReady(scope));

        using var first = readiness.BeginPending(scope, TestDescriptors.Key("first"));
        using var second = readiness.BeginPending(scope, TestDescriptors.Key("second"));
        Assert.False(readiness.IsReady(scope));

        var wait = readiness.WhenReadyAsync(scope, TestContext.Current.CancellationToken);
        Assert.False(wait.IsCompleted);
        first.Dispose();
        Assert.False(wait.IsCompleted);
        second.Dispose();
        await wait;
        Assert.True(readiness.IsReady(scope));
        Assert.Contains(changes, change => !change.IsReady);
        Assert.Contains(changes, change => change.IsReady);
    }

    [Fact]
    public async Task ReadinessWaitCanBeCanceled()
    {
        var readiness = new LierneReadinessService();
        var scope = new LierneReadinessScope("tests.document");
        using var pending = readiness.BeginPending(scope, TestDescriptors.Key("loading"));
        using var cancellation = new CancellationTokenSource();

        var wait = readiness.WhenReadyAsync(scope, cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
    }

    [Fact]
    public void EventBusDeliversAndStopsAfterDispose()
    {
        var bus = new LierneEventBus();
        var received = new List<int>();
        using var failing = bus.Subscribe<int>(_ => throw new InvalidOperationException("Test failure."));
        using var subscription = bus.Subscribe<int>(received.Add);

        bus.Publish(1);
        bus.Publish(2);
        subscription.Dispose();
        bus.Publish(3);

        Assert.Equal(new[] { 1, 2 }, received);
    }

    [Fact]
    public async Task ProgressReportsCancelAndComplete()
    {
        var progress = new LierneProgressService();
        var updates = new List<LierneProgressUpdate>();
        progress.Changed += updates.Add;

        var handle = progress.Start(TestDescriptors.Key("work"));
        handle.Report(0.5, TestDescriptors.Key("half"));
        handle.Cancel();
        await handle.DisposeAsync();

        Assert.True(handle.CancellationToken.IsCancellationRequested);
        Assert.Contains(updates, update => update.State == LierneProgressState.Started);
        Assert.Contains(updates, update => update.State == LierneProgressState.Reported && update.Fraction == 0.5);
        Assert.Contains(updates, update => update.State == LierneProgressState.Canceled);
        Assert.DoesNotContain(updates, update => update.State == LierneProgressState.Completed);
    }

    [Fact]
    public void DisposableBagReleasesResourcesInReverseOrder()
    {
        var order = new List<int>();
        var bag = new LierneDisposableBag();
        bag.Add(new ActionDisposable(() => order.Add(1)));
        bag.Add(new ActionDisposable(() => order.Add(2)));

        bag.Dispose();
        bag.Dispose();

        Assert.Equal(new[] { 2, 1 }, order);
    }

    [Fact]
    public async Task FileStorageRoundTripsTheCacheSection()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"lierne-cache-{Guid.NewGuid():N}");
        try
        {
            var storage = new LierneFileStateStorage(directory);
            await storage.WriteAsync(
                LierneStateSection.Cache, "{\"value\":1}", TestContext.Current.CancellationToken);
            Assert.Equal(
                "{\"value\":1}",
                await storage.ReadAsync(LierneStateSection.Cache, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void FoundationServicesAreRegisteredForEachScope()
    {
        var services = new ServiceCollection();
        services.AddLierne(options => options.AddPlugin(new TestPlugin("tests.foundations")));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<LierneReadinessService>(
            scope.ServiceProvider.GetRequiredService<ILierneReadinessService>());
        Assert.IsType<LierneProgressService>(
            scope.ServiceProvider.GetRequiredService<ILierneProgressService>());
        Assert.IsType<LierneEventBus>(
            scope.ServiceProvider.GetRequiredService<ILierneEventBus>());
    }

    private sealed class ActionDisposable(Action action) : IDisposable
    {
        public void Dispose() => action();
    }
}
