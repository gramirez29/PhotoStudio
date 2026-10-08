using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Infrastructure.Persistence;
using PhotoStudio.Infrastructure.Persistence.Outbox;
using PhotoStudio.Infrastructure.Policies;

namespace PhotoStudio.Infrastructure;

/// <summary>
/// Registers the infrastructure adapters in the dependency injection container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Health check tag used by the readiness endpoint.
    /// </summary>
    public const string ReadinessTag = "ready";

    /// <summary>
    /// Adds MongoDB persistence, the outbox processor, the policy provider, the index initializer and the MongoDB health check.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException">When the MongoDB environment variables are missing.</exception>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var mongoOptions = MongoDbOptions.FromEnvironment();

        services.AddSingleton(mongoOptions);
        services.AddSingleton(_ => MongoClientFactory.Create(mongoOptions));
        services.AddSingleton(provider => provider.GetRequiredService<IMongoClient>().GetDatabase(mongoOptions.DatabaseName));

        services.AddScoped<IBookingRepository, MongoBookingRepository>();
        services.AddSingleton<IBookingPolicyProvider, EnvironmentBookingPolicyProvider>();
        services.AddSingleton<OutboxProcessor>();

        services.AddHostedService<MongoIndexInitializer>();
        services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongodb", tags: [ReadinessTag]);

        return services;
    }
}
