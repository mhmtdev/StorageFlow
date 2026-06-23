using Microsoft.Extensions.DependencyInjection;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Core.Configuration;
using StorageFlow.Core.DependencyInjection;
using StorageFlow.Testing;

namespace StorageFlow.Tests.Component.Infrastructure;

internal static class ComponentTestHost
{
    internal static ServiceProvider Build(
        Action<StorageFlowOptions>? configure = null,
        Action<IServiceCollection>? configureServices = null,
        Action<InMemoryRegistrationBuilder>? configureProvider = null)
    {
        var services = new ServiceCollection();
        configureServices?.Invoke(services);
        services.AddStorageFlow(options =>
        {
            options.Providers.UseInMemory(configureProvider);
            configure?.Invoke(options);
        });
        return services.BuildServiceProvider();
    }

    internal static IStorageService Storage(this IServiceProvider services) =>
        services.GetRequiredService<IStorageService>();
}
