namespace MiniLibrary;

/// <summary>
/// Generic repository interface for data access operations.
/// Provides basic CRUD operations for entities.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Retrieves an entity by its unique identifier.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <returns>The entity if found, null otherwise.</returns>
    Task<T?> GetByIdAsync(string id);

    /// <summary>
    /// Retrieves all entities from the repository.
    /// </summary>
    /// <returns>A collection of all entities.</returns>
    Task<IEnumerable<T>> GetAllAsync();

    /// <summary>
    /// Adds a new entity to the repository.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    Task AddAsync(T entity);

    /// <summary>
    /// Updates an existing entity in the repository.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    Task UpdateAsync(T entity);

    /// <summary>
    /// Removes an entity from the repository.
    /// </summary>
    /// <param name="id">The identifier of the entity to remove.</param>
    Task DeleteAsync(string id);
}
