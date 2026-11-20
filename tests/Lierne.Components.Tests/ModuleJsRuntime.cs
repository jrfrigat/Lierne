namespace Lierne.Components.Tests;

// Static rendering never runs after-render registration, but Flare's module services still import
// their JS module; return a disposable reference so tearing down the provider succeeds.
#pragma warning disable BL0016 // The fake runtime has no circuit and cannot fail an interop call.
internal sealed class ModuleJsRuntime : Microsoft.JSInterop.IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        ValueTask.FromResult(identifier == "import" ? (TValue)(object)new Module() : default!);

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier,
        CancellationToken cancellationToken,
        object?[]? args) => InvokeAsync<TValue>(identifier, args);

    private sealed class Module : Microsoft.JSInterop.IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) => InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
#pragma warning restore BL0016
