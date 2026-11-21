using System.Diagnostics;

namespace Lierne;

/// <summary>Coordinates readiness gates for one Lierne host scope.</summary>
public sealed class LierneReadinessService : ILierneReadinessService
{
    private readonly Lock _lock = new();
    private readonly Dictionary<LierneReadinessScope, Gate> _gates = [];

    /// <inheritdoc />
    public event Action<LierneReadinessChange>? Changed;

    /// <inheritdoc />
    public bool IsReady(LierneReadinessScope scope)
    {
        Validate(scope);
        lock (_lock)
        {
            return !_gates.TryGetValue(scope, out var gate) || gate.Pending == 0;
        }
    }

    /// <inheritdoc />
    public Task WhenReadyAsync(LierneReadinessScope scope, CancellationToken cancellationToken = default)
    {
        Validate(scope);
        Task ready;
        lock (_lock)
        {
            if (!_gates.TryGetValue(scope, out var gate) || gate.Pending == 0)
            {
                return Task.CompletedTask;
            }

            ready = gate.Completion.Task;
        }

        return ready.WaitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public IDisposable BeginPending(LierneReadinessScope scope, StringKey reason)
    {
        Validate(scope);
        LierneReadinessChange? change = null;
        lock (_lock)
        {
            if (!_gates.TryGetValue(scope, out var gate))
            {
                gate = new Gate();
                _gates.Add(scope, gate);
            }

            if (gate.Pending++ == 0)
            {
                gate.Completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                change = new LierneReadinessChange(scope, false, reason);
            }
        }

        Notify(change);
        return new PendingToken(this, scope);
    }

    private void Complete(LierneReadinessScope scope)
    {
        LierneReadinessChange? change = null;
        lock (_lock)
        {
            if (!_gates.TryGetValue(scope, out var gate) || gate.Pending == 0)
            {
                return;
            }

            if (--gate.Pending == 0)
            {
                var completion = gate.Completion;
                _gates.Remove(scope);
                completion.TrySetResult();
                change = new LierneReadinessChange(scope, true, null);
            }
        }

        Notify(change);
    }

    private void Notify(LierneReadinessChange? change)
    {
        if (change is null)
        {
            return;
        }

        try
        {
            Changed?.Invoke(change);
        }
        catch (Exception exception)
        {
            Trace.TraceError("A Lierne readiness observer failed: {0}", exception);
        }
    }

    private static void Validate(LierneReadinessScope scope)
    {
        if (string.IsNullOrWhiteSpace(scope.Value))
        {
            throw new ArgumentException("A readiness scope needs a non-empty value.", nameof(scope));
        }
    }

    private sealed class Gate
    {
        public int Pending;
        public TaskCompletionSource Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class PendingToken(LierneReadinessService owner, LierneReadinessScope scope) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Complete(scope);
            }
        }
    }
}
