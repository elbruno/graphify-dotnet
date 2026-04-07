namespace MiniLibrary;

/// <summary>
/// Service layer for user-related operations.
/// Orchestrates business logic and delegates data access to the repository.
/// </summary>
public class UserService
{
    private readonly IRepository<User> _userRepository;

    /// <summary>
    /// Initializes a new instance of the UserService class.
    /// </summary>
    /// <param name="userRepository">The user repository for data access.</param>
    public UserService(IRepository<User> userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    /// <summary>
    /// Creates a new user in the system.
    /// </summary>
    /// <param name="email">The user's email address.</param>
    /// <param name="name">The user's full name.</param>
    /// <returns>The created user.</returns>
    public async Task<User> CreateUserAsync(string email, string name)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty", nameof(email));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty", nameof(name));

        // Check for duplicate email
        var existingUsers = await _userRepository.GetAllAsync();
        if (existingUsers.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"User with email {email} already exists");

        // Create and add user
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = email,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _userRepository.AddAsync(user);
        return user;
    }

    /// <summary>
    /// Retrieves a user by their identifier.
    /// </summary>
    /// <param name="id">The user identifier.</param>
    /// <returns>The user if found, null otherwise.</returns>
    public async Task<User?> GetUserAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("User ID cannot be empty", nameof(id));

        return await _userRepository.GetByIdAsync(id);
    }

    /// <summary>
    /// Updates a user's information.
    /// </summary>
    /// <param name="id">The user identifier.</param>
    /// <param name="email">The new email address.</param>
    /// <param name="name">The new name.</param>
    public async Task UpdateUserAsync(string id, string email, string name)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
            throw new InvalidOperationException($"User with ID {id} not found");

        user.Email = email;
        user.Name = name;

        await _userRepository.UpdateAsync(user);
    }

    /// <summary>
    /// Deactivates a user account.
    /// </summary>
    /// <param name="id">The user identifier.</param>
    public async Task DeactivateUserAsync(string id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
            throw new InvalidOperationException($"User with ID {id} not found");

        user.IsActive = false;
        await _userRepository.UpdateAsync(user);
    }

    /// <summary>
    /// Permanently deletes a user from the system.
    /// </summary>
    /// <param name="id">The user identifier.</param>
    public async Task DeleteUserAsync(string id)
    {
        await _userRepository.DeleteAsync(id);
    }

    /// <summary>
    /// Gets all active users in the system.
    /// </summary>
    /// <returns>A collection of active users.</returns>
    public async Task<IEnumerable<User>> GetActiveUsersAsync()
    {
        var allUsers = await _userRepository.GetAllAsync();
        return allUsers.Where(u => u.IsActive);
    }

    /// <summary>
    /// Validates that a user exists and is active.
    /// </summary>
    /// <param name="id">The user identifier.</param>
    /// <returns>True if the user exists and is active, false otherwise.</returns>
    public async Task<bool> IsUserActiveAsync(string id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        return user?.IsActive ?? false;
    }
}
