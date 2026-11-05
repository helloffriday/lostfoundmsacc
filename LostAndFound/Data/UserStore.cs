using System.Data.OleDb;
using LostAndFound.Models;

namespace LostAndFound.Data;

/// <summary>User accounts live in the Access database. Save() rewrites the Users table in one transaction.</summary>
public class UserStore
{
    public static string Folder => Database.Folder;

    public List<User> Users { get; private set; } = new();

    public UserStore()
    {
        Users = ReadAll();
    }

    private static List<User> ReadAll()
    {
        var list = new List<User>();
        using var conn = Database.Open();
        using var cmd = new OleDbCommand(
            "SELECT [Id], [Username], [FullName], [Role], [IsActive], [PasswordHash], [PasswordSalt], " +
            "[PasswordIterations], [CreatedAt], [LastSignIn], [RememberTokenHash], [RememberExpires] FROM [Users]", conn);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            Enum.TryParse<UserRole>(Database.Str(r, 3), out var role);
            list.Add(new User
            {
                Id = Guid.Parse(Database.Str(r, 0)),
                Username = Database.Str(r, 1),
                FullName = Database.Str(r, 2),
                Role = role,
                IsActive = !r.IsDBNull(4) && r.GetBoolean(4),
                PasswordHash = Database.Str(r, 5),
                PasswordSalt = Database.Str(r, 6),
                PasswordIterations = r.IsDBNull(7) ? 0 : r.GetInt32(7),
                CreatedAt = Database.Date(r, 8) ?? DateTime.Now,
                LastSignIn = Database.Date(r, 9),
                RememberTokenHash = Database.Str(r, 10),
                RememberExpires = Database.Date(r, 11)
            });
        }
        return list;
    }

    public void Save()
    {
        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();

        using (var clear = new OleDbCommand("DELETE FROM [Users]", conn, tx))
            clear.ExecuteNonQuery();

        const string sql =
            "INSERT INTO [Users] ([Id], [Username], [FullName], [Role], [IsActive], [PasswordHash], [PasswordSalt], " +
            "[PasswordIterations], [CreatedAt], [LastSignIn], [RememberTokenHash], [RememberExpires]) " +
            "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

        foreach (var u in Users)
        {
            using var insert = new OleDbCommand(sql, conn, tx);
            Database.AddText(insert, u.Id.ToString());
            Database.AddText(insert, u.Username);
            Database.AddText(insert, u.FullName);
            Database.AddText(insert, u.Role.ToString());
            Database.AddBool(insert, u.IsActive);
            Database.AddText(insert, u.PasswordHash);
            Database.AddText(insert, u.PasswordSalt);
            Database.AddInt(insert, u.PasswordIterations);
            Database.AddDate(insert, u.CreatedAt);
            Database.AddDate(insert, u.LastSignIn);
            Database.AddText(insert, u.RememberTokenHash);
            Database.AddDate(insert, u.RememberExpires);
            insert.ExecuteNonQuery();
        }

        tx.Commit();
    }
}
