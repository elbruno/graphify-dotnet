namespace MiniLibrary;

/// <summary>
/// Represents a user entity in the system.
/// </summary>
public class User
{
    /// <summary>
    /// Gets or sets the unique identifier for the user.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's full name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date and time when the user was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets whether the user account is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Validates the user's data.
    /// </summary>
    /// <returns>True if the user data is valid, false otherwise.</returns>
    public bool Validate()
    {
        return !string.IsNullOrWhiteSpace(Email) && 
               !string.IsNullOrWhiteSpace(Name) &&
               Email.Contains('@');
    }
}
