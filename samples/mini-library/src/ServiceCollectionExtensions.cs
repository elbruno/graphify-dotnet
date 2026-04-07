using Microsoft.Extensions.DependencyInjection;

namespace MiniLibrary;

/// <summary>
/// Extension methods for configuring MiniLibrary services in dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds MiniLibrary services to the service collection.
    /// Registers UserRepository and UserService with appropriate lifetimes.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddMiniLibrary(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        // Register repository as singleton (in-memory store)
        services.AddSingleton<IRepository<User>, UserRepository>();

        // Register service as scoped (per-request lifetime in web apps)
        services.AddScoped<UserService>();

        return services;
    }

    /// <summary>
    /// Adds MiniLibrary services with a custom repository implementation.
    /// Useful for testing or when using a different data store.
    /// </summary>
    /// <typeparam name="TRepository">The repository implementation type.</typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddMiniLibraryWithRepository<TRepository>(
        this IServiceCollection services)
        where TRepository : class, IRepository<User>
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        // Register custom repository
        services.AddSingleton<IRepository<User>, TRepository>();

        // Register service
        services.AddScoped<UserService>();

        return services;
    }
}
