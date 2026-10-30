using Microsoft.Extensions.DependencyInjection;
using PhotoStudio.Application.Abstractions;

namespace PhotoStudio.Infrastructure.Storage;

/// <summary>
/// Registers the object storage in the dependency injection container.
/// </summary>
public static class ObjectStorageExtensions
{
    /// <summary>
    /// Registers <see cref="IObjectStorage"/>: the S3 adapter when the storage variables are set, and a stand-in that answers
    /// "unavailable" when none is. The process starts either way.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException">When the variables are set only in part, or the address is not safe.</exception>
    public static IServiceCollection AddObjectStorage(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = ObjectStorageOptions.FromEnvironment();
        if (options is null)
        {
            services.AddSingleton<IObjectStorage, UnavailableObjectStorage>();
            return services;
        }

        services.AddSingleton(options);
        services.AddSingleton<IObjectStorage, S3ObjectStorage>();
        return services;
    }
}
