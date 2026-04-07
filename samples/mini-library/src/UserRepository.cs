namespace MiniLibrary;

/// <summary>
/// Repository implementation for User entities.
/// Provides data access operations for users using an in-memory store.
/// </summary>
public class UserRepository : IRepository<User>
{
    private readonly Dictionary<string, User> _users = new();
    private readonly object _lock = new();

    /// <summary>
    /// Retrieves a user by their unique identifier.
    /// </summary>
    /// <param name="id">The user identifier.</param>
    /// <returns>The user if found, null otherwise.</returns>
    public Task<User?> GetByIdAsync(string id)
    {
        lock (_lock)
        {
            _users.TryGetValue(id, out var user);
            return Task.FromResult(user);
        }
    }

    /// <summary>
    /// Retrieves all users from the repository.
    /// </summary>
    /// <returns>A collection of all users.</returns>
    public Task<IEnumerable<User>> GetAllAsync()
    {
        lock (_lock)
        {
            return Task.FromResult<IEnumerable<User>>(_users.Values.ToList());
        }
    }

    /// <summary>
    /// Adds a new user to the repository.
    /// </summary>
    /// <param name="entity">The user to add.</param>
    public Task AddAsync(User entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        if (!entity.Validate())
            throw new ArgumentException("Invalid user data", nameof(entity));

        lock (_lock)
        {
            if (_users.ContainsKey(entity.Id))
                throw new InvalidOperationException($"User with ID {entity.Id} already exists");

            _users[entity.Id] = entity;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Updates an existing user in the repository.
    /// </summary>
    /// <param name="entity">The user to update.</param>
    public Task UpdateAsync(User entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        if (!entity.Validate())
            throw new ArgumentException("Invalid user data", nameof(entity));

        lock (_lock)
        {
            if (!_users.ContainsKey(entity.Id))
                throw new InvalidOperationException($"User with ID {entity.Id} not found");

            _users[entity.Id] = entity;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes a user from the repository.
    /// </summary>
    /// <param name="id">The identifier of the user to remove.</param>
    public Task DeleteAsync(string id)
    {
        lock (_lock)
        {
            if (!_users.Remove(id))
                throw new InvalidOperationException($"User with ID {id} not found");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Finds users by email address.
    /// </summary>
    /// <param name="email">The email to search for.</param>
    /// <returns>A collection of matching users.</returns>
    public Task<IEnumerable<User>> FindByEmailAsync(string email)
    {
        lock (_lock)
        {
            var matches = _users.Values
                .Where(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return Task.FromResult<IEnumerable<User>>(matches);
        }
    }

    /// <summary>
    /// Gets the count of active users.
    /// </summary>
    /// <returns>The number of active users.</returns>
    public Task<int> GetActiveCountAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_users.Values.Count(u => u.IsActive));
        }
    }
}
