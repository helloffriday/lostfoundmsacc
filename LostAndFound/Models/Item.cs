namespace LostAndFound.Models;

public enum ItemType { Lost, Found }

public enum ItemStatus { Open, Returned, Closed }

public class Item
{
    public static readonly string[] Categories =
    {
        "Electronics", "Wallet / Cards", "Keys", "Bag", "Clothing",
        "Jewelry", "Documents", "Pet", "Other"
    };

    public Guid Id { get; set; } = Guid.NewGuid();
    public ItemType Type { get; set; } = ItemType.Lost;
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Other";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
    public DateTime DateReported { get; set; } = DateTime.Today;
    public string ContactName { get; set; } = "";
    public string ContactInfo { get; set; } = "";
    public ItemStatus Status { get; set; } = ItemStatus.Open;
    public string Notes { get; set; } = "";
    public DateTime? ResolvedDate { get; set; }

    // Audit trail (blank for items created before sign-in existed).
    public string CreatedBy { get; set; } = "";
    public DateTime? CreatedAt { get; set; }
    public string ModifiedBy { get; set; } = "";
    public DateTime? ModifiedAt { get; set; }
}
