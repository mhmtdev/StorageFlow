using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StorageFlow.Abstractions.Exceptions;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;
using StorageFlow.Core.Configuration;
using StorageFlow.Core.Naming;
using StorageFlow.Core.Delivery;
using StorageFlow.Core.Registry;

namespace StorageFlow.Core.DependencyInjection;

/// <summary>
/// Extension methods for registering StorageFlow services into <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers StorageFlow with the provided configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Delegate to configure providers, policies, and profiles.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStorageFlow(
        this IServiceCollection services,
        Action<StorageFlowOptions> configure)
    {
        var options = new StorageFlowOptions();
        configure(options);
        options.Flush();

        services.TryAddTransient<GuidNamingStrategy>();
        services.TryAddTransient<SeoNamingStrategy>();
        services.TryAddTransient<PatternNamingStrategy>();
        services.TryAddScoped<INamingStrategyResolver, NamingStrategyResolver>();
        services.TryAddSingleton<IObjectKeyValidator, ObjectKeyValidator>();
        services.TryAddSingleton<IDeliveryUrlResolver, DeliveryUrlResolver>();

        // Register options as singleton so pipeline and service can access it
        services.AddSingleton(options);

        // Build provider instances and registry
        services.AddSingleton<IStorageProviderRegistry>(sp =>
        {
            var providers = new Dictionary<StorageProviderToken, IStorageProvider>();

            foreach (var registration in options.Providers.Registrations.Values)
            {
                var provider = registration.Factory(sp);

                if (!string.Equals(
                        provider.ProviderName,
                        registration.Token.Name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new StorageConfigurationException(
                        $"Provider token '{registration.Token}' does not match implementation name " +
                        $"'{provider.ProviderName}'.");
                }

                providers[registration.Token] = provider;
            }

            return new StorageProviderRegistry(providers, options.Providers.DefaultProvider);
        });

        // Register policy overrides dictionary (provider token → overrides)
        services.AddSingleton<IReadOnlyDictionary<StorageProviderToken, ProviderPolicyOverrides>>(_ =>
        {
            return options.Providers.Registrations.Values.ToDictionary(
                registration => registration.Token,
                registration => new ProviderPolicyOverrides(
                    registration.ValidationPolicies,
                    registration.NamingPolicies,
                    registration.PresignedUrlPolicies,
                    registration.DeliveryUrlPolicies));
        });

        // Register IStorageService (IPresignedUrlCache is optional — registered by extension packages)
        services.AddScoped<IStorageService>(sp =>
            new StorageService(
                sp.GetRequiredService<IStorageProviderRegistry>(),
                sp.GetRequiredService<StorageFlowOptions>(),
                sp.GetRequiredService<IReadOnlyDictionary<StorageProviderToken, ProviderPolicyOverrides>>(),
                sp.GetRequiredService<INamingStrategyResolver>(),
                sp.GetRequiredService<IObjectKeyValidator>(),
                sp.GetRequiredService<IDeliveryUrlResolver>(),
                sp.GetService<IPresignedUrlCache>(),
                sp.GetServices<IFileValidator>()));

        return services;
    }

    /// <summary>
    /// Registers an application-defined naming strategy for use by typed naming policies.
    /// </summary>
    public static IServiceCollection AddStorageFlowNamingStrategy<TStrategy>(
        this IServiceCollection services)
        where TStrategy : class, IFileNamingStrategy
    {
        services.TryAddTransient<TStrategy>();
        return services;
    }

    /// <summary>
    /// Registers an application-defined validator for ordered execution in validation pipelines.
    /// </summary>
    public static IServiceCollection AddStorageFlowValidator<TValidator>(
        this IServiceCollection services)
        where TValidator : class, IFileValidator
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Transient<IFileValidator, TValidator>());
        return services;
    }
}
