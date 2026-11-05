namespace LostAndFound.Models;

public enum UserRole { User, Admin }

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.User;

    /// <summary>False while an account is waiting for approval, or after an admin disables it.</summary>
    public bool IsActive { get; set; } = true;

    // Password is never stored: only a salted PBKDF2 hash.
    public string PasswordHash { get; set; } = "";
    public string PasswordSalt { get; set; } = "";
    public int PasswordIterations { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastSignIn { get; set; }

    // "Keep me signed in": SHA-256 of a random token. The token itself lives in session.json.
    public string RememberTokenHash { get; set; } = "";
    public DateTime? RememberExpires { get; set; }
}
