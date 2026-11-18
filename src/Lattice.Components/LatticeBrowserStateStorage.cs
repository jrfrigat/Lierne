using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.JSInterop;

namespace Lattice;

/// <summary>Registers browser-backed Lattice session storage for an interactive web host.</summary>
public static class LatticeBrowserStateStorageExtensions
{
    /// <summary>Adds a store that keeps Lattice state on the current browser and origin.</summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="applicationKey">A stable key chosen by the host to isolate its state.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddLatticeBrowserStateStorage(
        this IServiceCollection services, string applicationKey)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationKey);
        services.TryAddScoped<ILatticeStateStorage>(provider =>
            new BrowserStateStorage(provider.GetRequiredService<IJSRuntime>(), applicationKey));
        return services;
    }

    private sealed class BrowserStateStorage(IJSRuntime js, string applicationKey) : ILatticeStateStorage
    {
        private readonly string _prefix = $"lattice:{applicationKey}:";

        public async ValueTask<string?> ReadAsync(LatticeStateSection section, CancellationToken cancellationToken)
        {
            try
            {
                return await js.InvokeAsync<string?>("localStorage.getItem", cancellationToken, _prefix + section);
            }
            catch (JSException exception)
            {
                throw new IOException("Could not read Lattice state from browser storage.", exception);
            }
        }

        public async ValueTask WriteAsync(
            LatticeStateSection section, string json, CancellationToken cancellationToken)
        {
            try
            {
                await js.InvokeVoidAsync("localStorage.setItem", cancellationToken, _prefix + section, json);
            }
            catch (JSException exception)
            {
                throw new IOException("Could not write Lattice state to browser storage.", exception);
            }
        }
    }
}
