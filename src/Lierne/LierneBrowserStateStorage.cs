using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.JSInterop;

namespace Lierne;

/// <summary>Registers browser-backed Lierne session storage for an interactive web host.</summary>
public static class LierneBrowserStateStorageExtensions
{
    /// <summary>Adds a store that keeps Lierne state on the current browser and origin.</summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="applicationKey">A stable key chosen by the host to isolate its state.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddLierneBrowserStateStorage(
        this IServiceCollection services, string applicationKey)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationKey);
        services.TryAddScoped<ILierneStateStorage>(provider =>
            new BrowserStateStorage(provider.GetRequiredService<IJSRuntime>(), applicationKey));
        return services;
    }

    private sealed class BrowserStateStorage(IJSRuntime js, string applicationKey) : ILierneStateStorage
    {
        // Keep the existing key prefix so renamed hosts can read their saved state.
        private readonly string _prefix = $"lattice:{applicationKey}:";

        public async ValueTask<string?> ReadAsync(LierneStateSection section, CancellationToken cancellationToken)
        {
            try
            {
                return await js.InvokeAsync<string?>("localStorage.getItem", cancellationToken, _prefix + section);
            }
            catch (JSException exception)
            {
                throw new IOException("Could not read Lierne state from browser storage.", exception);
            }
        }

        public async ValueTask WriteAsync(
            LierneStateSection section, string json, CancellationToken cancellationToken)
        {
            try
            {
                await js.InvokeVoidAsync("localStorage.setItem", cancellationToken, _prefix + section, json);
            }
            catch (JSException exception)
            {
                throw new IOException("Could not write Lierne state to browser storage.", exception);
            }
        }
    }
}
