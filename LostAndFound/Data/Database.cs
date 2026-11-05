using System.Data.OleDb;
using System.Runtime.InteropServices;

namespace LostAndFound.Data;

/// <summary>
/// Microsoft Access database (lostandfound.accdb), created automatically on first run.
/// Needs the 64-bit Microsoft Access Database Engine (ACE OLE DB provider) - the same one inventory.exe uses.
/// </summary>
public static class Database
{
    private static readonly object Gate = new();
    private static bool _ready;
    private static string? _provider;

    /// <summary>Per-Windows-user folder, used only for the "keep me signed in" token file.</summary>
    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LostAndFound");

    /// <summary>Next to the .exe if that folder is writable, otherwise inside <see cref="Folder"/>.</summary>
    public static string DbPath { get; } = ChooseDbPath();

    private static string ChooseDbPath()
    {
        var beside = Path.Combine(AppContext.BaseDirectory, "lostandfound.accdb");
        var appData = Path.Combine(Folder, "lostandfound.accdb");
        if (File.Exists(beside)) return beside;
        if (File.Exists(appData)) return appData;
        try
        {
            var probe = Path.Combine(AppContext.BaseDirectory, ".write-test-" + Guid.NewGuid().ToString("N"));
            using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
            return beside;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return appData;
        }
    }

    private static string Provider
    {
        get
        {
            if (_provider != null) return _provider;
            foreach (var name in new[] { "Microsoft.ACE.OLEDB.12.0", "Microsoft.ACE.OLEDB.16.0" })
            {
                if (Type.GetTypeFromProgID(name) != null) return _provider = name;
            }
            throw new InvalidOperationException(
                "The Microsoft Access Database Engine (ACE OLE DB provider) is not installed.");
        }
    }

    private static string ConnectionString =>
        $"Provider={Provider};Data Source={DbPath};Persist Security Info=False;";

    // Access SQL: every identifier is bracketed because words like Name, Role and Status are reserved.
    // Text columns are nullable and empty strings are stored as NULL (Access rejects zero-length text by default).
    private static readonly (string Table, string[] Statements)[] Tables =
    {
        ("Users", new[]
        {
            "CREATE TABLE [Users] (" +
            "[Id] TEXT(36) PRIMARY KEY, [Username] TEXT(50), [FullName] TEXT(100), [Role] TEXT(20), " +
            "[IsActive] YESNO, [PasswordHash] TEXT(255), [PasswordSalt] TEXT(255), [PasswordIterations] LONG, " +
            "[CreatedAt] DATETIME, [LastSignIn] DATETIME, [RememberTokenHash] TEXT(255), [RememberExpires] DATETIME)",
            "CREATE UNIQUE INDEX [idxUsername] ON [Users] ([Username])"
        }),
        ("Items", new[]
        {
            "CREATE TABLE [Items] (" +
            "[Id] TEXT(36) PRIMARY KEY, [ItemType] TEXT(20), [Name] TEXT(255), [Category] TEXT(50), " +
            "[Description] LONGTEXT, [Location] TEXT(255), [DateReported] DATETIME, [ContactName] TEXT(255), " +
            "[ContactInfo] TEXT(255), [Status] TEXT(20), [Notes] LONGTEXT, [ResolvedDate] DATETIME, " +
            "[CreatedBy] TEXT(50), [CreatedAt] DATETIME, [ModifiedBy] TEXT(50), [ModifiedAt] DATETIME)"
        })
    };

    /// <summary>Opens a connection. The caller disposes it.</summary>
    public static OleDbConnection Open()
    {
        EnsureCreated();
        return Connect();
    }

    public static void EnsureCreated()
    {
        lock (Gate)
        {
            if (_ready) return;
            Directory.CreateDirectory(Folder);
            Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
            if (!File.Exists(DbPath)) CreateDatabaseFile();

            using var conn = Connect();
            foreach (var (table, statements) in Tables)
            {
                if (TableExists(conn, table)) continue;
                foreach (var sql in statements)
                {
                    using var cmd = new OleDbCommand(sql, conn);
                    cmd.ExecuteNonQuery();
                }
            }
            _ready = true;
        }
    }

    private static OleDbConnection Connect()
    {
        var conn = new OleDbConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    private static void CreateDatabaseFile()
    {
        var type = Type.GetTypeFromProgID("ADOX.Catalog")
            ?? throw new InvalidOperationException("ADOX is not available (it ships with the Access Database Engine).");
        dynamic catalog = Activator.CreateInstance(type)!;
        try { catalog.Create(ConnectionString); }
        finally { Marshal.FinalReleaseComObject((object)catalog); }
    }

    private static bool TableExists(OleDbConnection conn, string name)
    {
        using var schema = conn.GetSchema("Tables", new string?[] { null, null, name, "TABLE" });
        return schema.Rows.Count > 0;
    }

    // ---- parameter helpers (OleDb parameters are positional: add them in the order of the ? marks) ----

    public static void AddText(OleDbCommand cmd, string? value, bool memo = false)
    {
        var p = cmd.Parameters.Add("p", memo ? OleDbType.LongVarWChar : OleDbType.VarWChar);
        if (string.IsNullOrEmpty(value)) { p.Size = 1; p.Value = DBNull.Value; }
        else { p.Size = value.Length; p.Value = value; }
    }

    public static void AddDate(OleDbCommand cmd, DateTime? value)
    {
        var p = cmd.Parameters.Add("p", OleDbType.Date);
        p.Value = value.HasValue ? value.Value : DBNull.Value;
    }

    public static void AddBool(OleDbCommand cmd, bool value) =>
        cmd.Parameters.Add("p", OleDbType.Boolean).Value = value;

    public static void AddInt(OleDbCommand cmd, int value) =>
        cmd.Parameters.Add("p", OleDbType.Integer).Value = value;

    // ---- reader helpers ----

    public static string Str(OleDbDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);

    public static DateTime? Date(OleDbDataReader r, int i) => r.IsDBNull(i) ? null : r.GetDateTime(i);
}
