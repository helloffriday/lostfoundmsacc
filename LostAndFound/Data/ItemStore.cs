using System.Data.OleDb;
using LostAndFound.Models;

namespace LostAndFound.Data;

/// <summary>Items live in the Access database. Save() rewrites the Items table in one transaction.</summary>
public class ItemStore
{
    public List<Item> Items { get; private set; } = new();

    public ItemStore()
    {
        Items = ReadAll();
    }

    private static List<Item> ReadAll()
    {
        var list = new List<Item>();
        using var conn = Database.Open();
        using var cmd = new OleDbCommand(
            "SELECT [Id], [ItemType], [Name], [Category], [Description], [Location], [DateReported], " +
            "[ContactName], [ContactInfo], [Status], [Notes], [ResolvedDate], [CreatedBy], [CreatedAt], " +
            "[ModifiedBy], [ModifiedAt] FROM [Items]", conn);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            Enum.TryParse<ItemType>(Database.Str(r, 1), out var type);
            Enum.TryParse<ItemStatus>(Database.Str(r, 9), out var status);
            list.Add(new Item
            {
                Id = Guid.Parse(Database.Str(r, 0)),
                Type = type,
                Name = Database.Str(r, 2),
                Category = Database.Str(r, 3),
                Description = Database.Str(r, 4),
                Location = Database.Str(r, 5),
                DateReported = Database.Date(r, 6) ?? DateTime.Today,
                ContactName = Database.Str(r, 7),
                ContactInfo = Database.Str(r, 8),
                Status = status,
                Notes = Database.Str(r, 10),
                ResolvedDate = Database.Date(r, 11),
                CreatedBy = Database.Str(r, 12),
                CreatedAt = Database.Date(r, 13),
                ModifiedBy = Database.Str(r, 14),
                ModifiedAt = Database.Date(r, 15)
            });
        }
        return list;
    }

    public void Save()
    {
        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();

        using (var clear = new OleDbCommand("DELETE FROM [Items]", conn, tx))
            clear.ExecuteNonQuery();

        const string sql =
            "INSERT INTO [Items] ([Id], [ItemType], [Name], [Category], [Description], [Location], [DateReported], " +
            "[ContactName], [ContactInfo], [Status], [Notes], [ResolvedDate], [CreatedBy], [CreatedAt], " +
            "[ModifiedBy], [ModifiedAt]) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

        foreach (var i in Items)
        {
            using var insert = new OleDbCommand(sql, conn, tx);
            Database.AddText(insert, i.Id.ToString());
            Database.AddText(insert, i.Type.ToString());
            Database.AddText(insert, i.Name);
            Database.AddText(insert, i.Category);
            Database.AddText(insert, i.Description, memo: true);
            Database.AddText(insert, i.Location);
            Database.AddDate(insert, i.DateReported);
            Database.AddText(insert, i.ContactName);
            Database.AddText(insert, i.ContactInfo);
            Database.AddText(insert, i.Status.ToString());
            Database.AddText(insert, i.Notes, memo: true);
            Database.AddDate(insert, i.ResolvedDate);
            Database.AddText(insert, i.CreatedBy);
            Database.AddDate(insert, i.CreatedAt);
            Database.AddText(insert, i.ModifiedBy);
            Database.AddDate(insert, i.ModifiedAt);
            insert.ExecuteNonQuery();
        }

        tx.Commit();
    }
}
