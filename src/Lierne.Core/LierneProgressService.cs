using System.Diagnostics;

namespace Lierne;

/// <summary>Starts progress operations for one Lierne host scope.</summary>
public sealed class LierneProgressService : ILierneProgressService
{
    /// <inheritdoc />
    public event Action<LierneProgressUpdate>? Changed;

    /// <inheritdoc />
    public ILierneProgressHandle Start(
        StringKey title,
        LierneProgressKind kind = LierneProgressKind.Background) =>
        new Handle(this, title, kind);

    private void Publish(LierneProgressUpdate update)
    {
        try
        {
            Changed?.Invoke(update);
        }
        catch (Exception exception)
        {
            Trace.TraceError("A Lierne progress observer failed: {0}", exception);
        }
    }

    private sealed class Handle : ILierneProgressHandle
    {
        private readonly LierneProgressService _owner;
        private readonly StringKey _title;
        private readonly LierneProgressKind _kind;
        private readonly CancellationTokenSource _cancellation = new();
        private readonly CancellationToken _token;
        private readonly Lock _lock = new();
        private LierneProgressState _state = LierneProgressState.Started;
        private double? _fraction;
        private StringKey? _detail;

        public Handle(LierneProgressService owner, StringKey title, LierneProgressKind kind)
        {
            _owner = owner;
            _title = title;
            _kind = kind;
            _token = _cancellation.Token;
            _owner.Publish(new LierneProgressUpdate(_title, _kind, LierneProgressState.Started));
        }

        public CancellationToken CancellationToken => _token;

        public void Report(double? fraction, StringKey? detail = null)
        {
            if (fraction is < 0 or > 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fraction), "A progress fraction must be between 0 and 1.");
            }

            lock (_lock)
            {
                if (_state is LierneProgressState.Completed or LierneProgressState.Canceled)
                {
                    return;
                }

                _state = LierneProgressState.Reported;
                _fraction = fraction;
                _detail = detail;
            }

            _owner.Publish(new LierneProgressUpdate(
                _title, _kind, LierneProgressState.Reported, fraction, detail));
        }

        public void Cancel()
        {
            LierneProgressUpdate? update = null;
            lock (_lock)
            {
                if (_state is LierneProgressState.Completed or LierneProgressState.Canceled)
                {
                    return;
                }

                _state = LierneProgressState.Canceled;
                update = new LierneProgressUpdate(
                    _title, _kind, LierneProgressState.Canceled, _fraction, _detail);
            }

            _cancellation.Cancel();
            if (update is not null)
            {
                _owner.Publish(update);
            }
        }

        public ValueTask DisposeAsync()
        {
            LierneProgressUpdate? update = null;
            lock (_lock)
            {
                if (_state is not LierneProgressState.Completed and not LierneProgressState.Canceled)
                {
                    _state = LierneProgressState.Completed;
                    update = new LierneProgressUpdate(
                        _title, _kind, LierneProgressState.Completed, _fraction, _detail);
                }
            }

            if (update is not null)
            {
                _owner.Publish(update);
            }

            _cancellation.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
