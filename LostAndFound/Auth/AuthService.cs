using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LostAndFound.Data;
using LostAndFound.Models;

namespace LostAndFound.Auth;

/// <summary>Registration, sign-in, "keep me signed in", sign-out and user administration.</summary>
public class AuthService
{
    public const int MinPasswordLength = 8;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutTime = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RememberFor = TimeSpan.FromDays(30);
    private static readonly Regex UsernamePattern = new(@"^[A-Za-z0-9._-]{3,30}$");

    private class SessionFile
    {
        public string Username { get; set; } = "";
        public string Token { get; set; } = "";
    }

    private readonly UserStore _store;
    private readonly string _sessionPath;
    private readonly Dictionary<string, (int Count, DateTime LockedUntil)> _failures =
        new(StringComparer.OrdinalIgnoreCase);

    public AuthService(UserStore store)
    {
        _store = store;
        _sessionPath = Path.Combine(UserStore.Folder, "session.json");
    }

    public User? Current { get; private set; }
    public bool IsAdmin => Current?.Role == UserRole.Admin;
    public bool HasUsers => _store.Users.Count > 0;
    public IReadOnlyList<User> Users => _store.Users;

    // ---------------------------------------------------------------- sign in / out

    public (User? User, string? Error) SignIn(string username, string password, bool remember)
    {
        username = username.Trim();
        if (username.Length == 0 || password.Length == 0)
            return (null, "Please enter your username and password.");

        if (_failures.TryGetValue(username, out var f) && f.LockedUntil > DateTime.Now)
        {
            int secs = (int)Math.Ceiling((f.LockedUntil - DateTime.Now).TotalSeconds);
            return (null, $"Too many failed attempts. Try again in {secs} seconds.");
        }

        var user = Find(username);
        bool ok;
        if (user != null)
            ok = PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt, user.PasswordIterations);
        else
        {
            PasswordHasher.Hash(password); // same cost as a real check, so timing doesn't reveal valid usernames
            ok = false;
        }

        if (!ok || user == null)
        {
            RecordFailure(username);
            return (null, "Invalid username or password.");
        }

        if (!user.IsActive)
            return (null, "This account is waiting for administrator approval, or has been disabled.");

        _failures.Remove(username);
        user.LastSignIn = DateTime.Now;
        Current = user;
        if (remember) IssueRememberToken(user); else ClearRemember(user);
        _store.Save();
        return (user, null);
    }

    /// <summary>Signs in silently if the user previously ticked "Keep me signed in".</summary>
    public User? TryAutoSignIn()
    {
        var session = ReadSession();
        if (session == null) return null;

        var user = Find(session.Username);
        if (user == null || !user.IsActive || user.RememberTokenHash.Length == 0 || user.RememberExpires == null)
        {
            DeleteSession();
            return null;
        }
        if (user.RememberExpires.Value < DateTime.Now)
        {
            DeleteSession();
            return null;
        }

        var presented = Encoding.UTF8.GetBytes(HashToken(session.Token));
        var stored = Encoding.UTF8.GetBytes(user.RememberTokenHash);
        if (!CryptographicOperations.FixedTimeEquals(presented, stored))
        {
            DeleteSession();
            return null;
        }

        Current = user;
        user.LastSignIn = DateTime.Now;
        IssueRememberToken(user); // rotate token and extend the 30 days
        _store.Save();
        return user;
    }

    public void SignOut()
    {
        if (Current != null)
        {
            ClearRemember(Current);
            _store.Save();
        }
        Current = null;
        DeleteSession();
    }

    // ---------------------------------------------------------------- registration

    public (bool Ok, string Message, User? User) Register(string fullName, string username, string password, string confirm)
    {
        fullName = fullName.Trim();
        username = username.Trim();

        if (fullName.Length == 0 || username.Length == 0 || password.Length == 0 || confirm.Length == 0)
            return (false, "Please fill in all fields.", null);
        if (!UsernamePattern.IsMatch(username))
            return (false, "Username must be 3-30 characters: letters, numbers, dot, dash or underscore.", null);
        var pwError = ValidatePassword(password);
        if (pwError != null) return (false, pwError, null);
        if (password != confirm) return (false, "Passwords do not match.", null);
        if (Find(username) != null) return (false, "That username is already taken.", null);

        // The very first account becomes the administrator. Later sign-ups wait for approval.
        bool first = !HasUsers;
        var (hash, salt, iterations) = PasswordHasher.Hash(password);
        var user = new User
        {
            Username = username,
            FullName = fullName,
            Role = first ? UserRole.Admin : UserRole.User,
            IsActive = first,
            PasswordHash = hash,
            PasswordSalt = salt,
            PasswordIterations = iterations
        };
        _store.Users.Add(user);
        _store.Save();

        return (true, first
            ? "Administrator account created. You can now sign in."
            : "Account created. An administrator must approve it before you can sign in.", user);
    }

    public static string? ValidatePassword(string password) =>
        password.Length < MinPasswordLength
            ? $"Password must be at least {MinPasswordLength} characters."
            : null;

    // ---------------------------------------------------------------- passwords

    public string? ChangeOwnPassword(string currentPassword, string newPassword)
    {
        var me = Current;
        if (me == null) return "You are not signed in.";
        if (!PasswordHasher.Verify(currentPassword, me.PasswordHash, me.PasswordSalt, me.PasswordIterations))
            return "Current password is incorrect.";
        var err = ValidatePassword(newPassword);
        if (err != null) return err;
        SetPassword(me, newPassword);
        _store.Save();
        return null;
    }

    public string? ResetPassword(User target, string newPassword)
    {
        if (!IsAdmin) return "Administrator access required.";
        var err = ValidatePassword(newPassword);
        if (err != null) return err;
        SetPassword(target, newPassword);
        ClearRememberToken(target); // force a fresh sign-in with the new password
        _store.Save();
        return null;
    }

    // ---------------------------------------------------------------- administration

    public string? SetActive(User target, bool active)
    {
        if (!IsAdmin) return "Administrator access required.";
        if (!active)
        {
            if (target.Id == Current!.Id) return "You can't disable your own account.";
            if (IsLastActiveAdmin(target)) return "At least one active administrator is required.";
        }
        target.IsActive = active;
        if (!active) ClearRememberToken(target);
        _store.Save();
        return null;
    }

    public string? SetRole(User target, UserRole role)
    {
        if (!IsAdmin) return "Administrator access required.";
        if (role != UserRole.Admin)
        {
            if (target.Id == Current!.Id) return "You can't remove your own administrator role.";
            if (IsLastActiveAdmin(target)) return "At least one active administrator is required.";
        }
        target.Role = role;
        _store.Save();
        return null;
    }

    public string? DeleteUser(User target)
    {
        if (!IsAdmin) return "Administrator access required.";
        if (target.Id == Current!.Id) return "You can't delete your own account.";
        if (IsLastActiveAdmin(target)) return "At least one active administrator is required.";
        _store.Users.Remove(target);
        _store.Save();
        return null;
    }

    // ---------------------------------------------------------------- helpers

    private User? Find(string username) =>
        _store.Users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

    private bool IsLastActiveAdmin(User target) =>
        target.Role == UserRole.Admin && target.IsActive &&
        !_store.Users.Any(u => u.Id != target.Id && u.Role == UserRole.Admin && u.IsActive);

    private static void SetPassword(User user, string password)
    {
        var (hash, salt, iterations) = PasswordHasher.Hash(password);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.PasswordIterations = iterations;
    }

    private void RecordFailure(string username)
    {
        _failures.TryGetValue(username, out var f);
        int count = f.Count + 1;
        _failures[username] = count >= MaxFailedAttempts
            ? (0, DateTime.Now + LockoutTime)
            : (count, DateTime.MinValue);
    }

    // ---- remember-me token

    private static string HashToken(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private void IssueRememberToken(User user)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        user.RememberTokenHash = HashToken(token);
        user.RememberExpires = DateTime.Now + RememberFor;
        WriteSession(new SessionFile { Username = user.Username, Token = token });
    }

    private void ClearRemember(User user)
    {
        ClearRememberToken(user);
        DeleteSession();
    }

    private static void ClearRememberToken(User user)
    {
        user.RememberTokenHash = "";
        user.RememberExpires = null;
    }

    private SessionFile? ReadSession()
    {
        try
        {
            if (!File.Exists(_sessionPath)) return null;
            var s = JsonSerializer.Deserialize<SessionFile>(File.ReadAllText(_sessionPath));
            return s == null || s.Username.Length == 0 || s.Token.Length == 0 ? null : s;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void WriteSession(SessionFile s)
    {
        try { File.WriteAllText(_sessionPath, JsonSerializer.Serialize(s)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* just won't be remembered */ }
    }

    private void DeleteSession()
    {
        try { if (File.Exists(_sessionPath)) File.Delete(_sessionPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
