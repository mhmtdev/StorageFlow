using Microsoft.Extensions.DependencyInjection;
using StorageFlow.Abstractions.Configuration;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Core.Configuration;
using StorageFlow.Core.DependencyInjection;

namespace StorageFlow.Tests.Integration.Infrastructure;

internal sealed class ProviderServiceFactory
{
    internal static ServiceProvider Build(
        Action<ProviderCollectionBuilder> configureProvider,
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        configureServices?.Invoke(services);
        services.AddStorageFlow(options => Configure(options, configureProvider));
        return services.BuildServiceProvider();
    }

    private static void Configure(
        StorageFlowOptions options,
        Action<ProviderCollectionBuilder> configureProvider)
    {
        options.Naming
            .AddPolicy<IntegrationNaming>(policy =>
                policy.UsePattern("integration/{guid}{ext}"))
            .AsDefault();
        options.PresignedUrls.AddPolicy<IntegrationDownloadPolicy>(policy =>
        {
            policy.HttpMethod = HttpMethod.Get;
            policy.Expiration = TimeSpan.FromMinutes(5);
        });
        configureProvider(options.Providers);
    }

    internal static IStorageService Storage(IServiceProvider services) =>
        services.GetRequiredService<IStorageService>();
}
