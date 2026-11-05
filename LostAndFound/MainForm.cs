using System.Text;
using System.Text.RegularExpressions;
using LostAndFound.Auth;
using LostAndFound.Data;
using LostAndFound.Models;

namespace LostAndFound;

public class MainForm : Form
{
    private readonly AuthService _auth;
    private readonly ItemStore _store = new();
    private Button _deleteButton = null!;

    /// <summary>True when the window closed because the user clicked Logout (so Program shows sign-in again).</summary>
    public bool LoggedOut { get; private set; }

    private readonly DataGridView _grid = new();
    private readonly TextBox _search = new() { PlaceholderText = "Search name, description, location, contact…", Width = 320 };
    private readonly ComboBox _typeFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private readonly ComboBox _statusFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    private readonly ToolStripStatusLabel _count = new();

    public MainForm(AuthService auth)
    {
        _auth = auth;
        if (_auth.Current == null) throw new InvalidOperationException("MainForm requires a signed-in user.");

        Text = "Lost & Found Management";
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1000, 600);
        MinimumSize = new Size(760, 400);
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        RefreshGrid();
    }

    private void BuildUi()
    {
        // Grid
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.MultiSelect = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.BackgroundColor = SystemColors.Window;
        foreach (var header in new[] { "Type", "Item", "Category", "Location", "Date", "Contact", "Added by", "Status" })
            _grid.Columns.Add(header, header);
        _grid.Columns["Item"]!.FillWeight = 150;
        _grid.Columns["Location"]!.FillWeight = 130;
        _grid.Columns["Type"]!.FillWeight = 60;
        _grid.Columns["Date"]!.FillWeight = 80;
        _grid.Columns["Status"]!.FillWeight = 70;
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelected(); };

        // Status bar
        var status = new StatusStrip();
        status.Items.Add(_count);

        // Filter bar
        _typeFilter.Items.Add("All");
        _typeFilter.Items.AddRange(Enum.GetNames<ItemType>());
        _typeFilter.SelectedIndex = 0;
        _statusFilter.Items.Add("All");
        _statusFilter.Items.AddRange(Enum.GetNames<ItemStatus>());
        _statusFilter.SelectedIndex = 0;
        _search.TextChanged += (_, _) => RefreshGrid();
        _typeFilter.SelectedIndexChanged += (_, _) => RefreshGrid();
        _statusFilter.SelectedIndexChanged += (_, _) => RefreshGrid();

        var filterBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 4, 8, 4) };
        filterBar.Controls.Add(new Label { Text = "Search", AutoSize = true, Margin = new Padding(3, 7, 3, 0) });
        filterBar.Controls.Add(_search);
        filterBar.Controls.Add(new Label { Text = "Type", AutoSize = true, Margin = new Padding(12, 7, 3, 0) });
        filterBar.Controls.Add(_typeFilter);
        filterBar.Controls.Add(new Label { Text = "Status", AutoSize = true, Margin = new Padding(12, 7, 3, 0) });
        filterBar.Controls.Add(_statusFilter);

        // Toolbar
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 8, 8, 0) };
        toolbar.Controls.Add(MakeButton("Add", (_, _) => AddItem()));
        toolbar.Controls.Add(MakeButton("Edit", (_, _) => EditSelected()));
        _deleteButton = MakeButton("Delete", (_, _) => DeleteSelected());
        _deleteButton.Enabled = _auth.IsAdmin; // only administrators may delete records
        toolbar.Controls.Add(_deleteButton);
        toolbar.Controls.Add(MakeButton("Mark Returned", (_, _) => MarkReturned()));
        toolbar.Controls.Add(MakeButton("Find Matches", (_, _) => FindMatches()));
        toolbar.Controls.Add(MakeButton("Export CSV", (_, _) => ExportCsv()));

        // Fill control first; last-added docks first.
        Controls.Add(_grid);
        Controls.Add(status);
        Controls.Add(filterBar);
        Controls.Add(toolbar);

        // Account bar (added last so it docks at the very top).
        var me = _auth.Current!;
        var accountBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8, 6, 8, 2),
            BackColor = SystemColors.ControlLight
        };
        accountBar.Controls.Add(new Label
        {
            Text = $"Signed in as {me.FullName} ({me.Role})",
            AutoSize = true,
            Margin = new Padding(3, 8, 16, 0)
        });
        if (_auth.IsAdmin)
            accountBar.Controls.Add(MakeButton("Manage Users", (_, _) => { using var f = new UsersForm(_auth); f.ShowDialog(this); }));
        accountBar.Controls.Add(MakeButton("Change Password", (_, _) => { using var f = new ChangePasswordForm(_auth); f.ShowDialog(this); }));
        accountBar.Controls.Add(MakeButton("Logout", (_, _) => Logout()));
        Controls.Add(accountBar);
    }

    private void Logout()
    {
        _auth.SignOut();
        LoggedOut = true;
        Close();
    }

    private void Stamp(Item item)
    {
        item.ModifiedBy = _auth.Current?.Username ?? "";
        item.ModifiedAt = DateTime.Now;
    }

    private static Button MakeButton(string text, EventHandler onClick)
    {
        var b = new Button { Text = text, AutoSize = true, Padding = new Padding(6, 2, 6, 2) };
        b.Click += onClick;
        return b;
    }

    private Item? Selected => _grid.CurrentRow?.Tag as Item;

    private IEnumerable<Item> FilteredItems()
    {
        ItemType? type = _typeFilter.SelectedIndex > 0
            ? Enum.Parse<ItemType>((string)_typeFilter.SelectedItem!) : null;
        ItemStatus? status = _statusFilter.SelectedIndex > 0
            ? Enum.Parse<ItemStatus>((string)_statusFilter.SelectedItem!) : null;
        var q = _search.Text.Trim();

        return _store.Items
            .Where(i => type == null || i.Type == type)
            .Where(i => status == null || i.Status == status)
            .Where(i => q.Length == 0 ||
                        new[] { i.Name, i.Description, i.Location, i.Category, i.ContactName, i.ContactInfo, i.Notes }
                            .Any(f => f.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(i => i.DateReported);
    }

    private void RefreshGrid()
    {
        _grid.Rows.Clear();
        int shown = 0;
        foreach (var item in FilteredItems())
        {
            int idx = _grid.Rows.Add(
                item.Type.ToString(), item.Name, item.Category, item.Location,
                item.DateReported.ToString("yyyy-MM-dd"), item.ContactName,
                item.CreatedBy.Length == 0 ? "-" : item.CreatedBy, item.Status.ToString());
            var row = _grid.Rows[idx];
            row.Tag = item;
            if (item.Status != ItemStatus.Open)
                row.DefaultCellStyle.ForeColor = Color.Gray;
            shown++;
        }

        int openLost = _store.Items.Count(i => i.Type == ItemType.Lost && i.Status == ItemStatus.Open);
        int openFound = _store.Items.Count(i => i.Type == ItemType.Found && i.Status == ItemStatus.Open);
        _count.Text = $"{shown} shown   |   Open: {openLost} lost, {openFound} found   |   Total: {_store.Items.Count}";
    }

    private void AddItem()
    {
        using var form = new ItemForm();
        if (form.ShowDialog(this) != DialogResult.OK) return;
        form.Item.CreatedBy = _auth.Current?.Username ?? "";
        form.Item.CreatedAt = DateTime.Now;
        Stamp(form.Item);
        _store.Items.Add(form.Item);
        _store.Save();
        RefreshGrid();
    }

    private void EditSelected()
    {
        var item = Selected;
        if (item == null) { NothingSelected(); return; }
        using var form = new ItemForm(item);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        Stamp(item);
        _store.Save();
        RefreshGrid();
    }

    private void DeleteSelected()
    {
        if (!_auth.IsAdmin) return;
        var item = Selected;
        if (item == null) { NothingSelected(); return; }
        var answer = MessageBox.Show(this, $"Delete \"{item.Name}\"? This cannot be undone.", "Confirm delete",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes) return;
        _store.Items.Remove(item);
        _store.Save();
        RefreshGrid();
    }

    private void MarkReturned()
    {
        var item = Selected;
        if (item == null) { NothingSelected(); return; }
        item.Status = ItemStatus.Returned;
        item.ResolvedDate = DateTime.Today;
        Stamp(item);
        _store.Save();
        RefreshGrid();
    }

    private void FindMatches()
    {
        var item = Selected;
        if (item == null) { NothingSelected(); return; }

        var matches = _store.Items
            .Where(o => o.Type != item.Type && o.Status == ItemStatus.Open)
            .Select(o => (Item: o, Score: Score(item, o)))
            .Where(x => x.Score >= 3)
            .OrderByDescending(x => x.Score)
            .Take(5)
            .ToList();

        if (matches.Count == 0)
        {
            MessageBox.Show(this, "No likely matches found among open items.", "Find Matches",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var sb = new StringBuilder($"Possible matches for \"{item.Name}\":\n\n");
        foreach (var (m, score) in matches)
            sb.AppendLine($"• [{m.Type}] {m.Name} — {m.Location}, {m.DateReported:yyyy-MM-dd} (score {score})\n   Contact: {m.ContactName} {m.ContactInfo}");
        MessageBox.Show(this, sb.ToString(), "Find Matches", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static int Score(Item a, Item b)
    {
        int score = 0;
        if (a.Category == b.Category) score += 2;
        if (a.Location.Length > 0 && b.Location.Length > 0 &&
            (a.Location.Contains(b.Location, StringComparison.OrdinalIgnoreCase) ||
             b.Location.Contains(a.Location, StringComparison.OrdinalIgnoreCase)))
            score += 1;
        score += Math.Min(3, Tokens(a).Intersect(Tokens(b)).Count());
        return score;
    }

    private static HashSet<string> Tokens(Item i) =>
        Regex.Split($"{i.Name} {i.Description}".ToLowerInvariant(), @"[^\p{L}\p{N}]+")
            .Where(w => w.Length >= 3)
            .ToHashSet();

    private void ExportCsv()
    {
        using var dialog = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = "lost-and-found.csv" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var sb = new StringBuilder("Type,Name,Category,Description,Location,Date,ContactName,ContactInfo,Status,ResolvedDate,Notes,AddedBy,LastModifiedBy\n");
        foreach (var i in FilteredItems())
        {
            sb.AppendLine(string.Join(",", new[]
            {
                i.Type.ToString(), i.Name, i.Category, i.Description, i.Location,
                i.DateReported.ToString("yyyy-MM-dd"), i.ContactName, i.ContactInfo,
                i.Status.ToString(), i.ResolvedDate?.ToString("yyyy-MM-dd") ?? "", i.Notes,
                i.CreatedBy, i.ModifiedBy
            }.Select(Csv)));
        }
        File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
    }

    // Cells starting with = @ (or +/- followed by a non-number) would run as formulas when opened in Excel.
    private static string Csv(string s)
    {
        if (Regex.IsMatch(s, @"^([=@\t\r]|[+-][^\d\s(])")) s = "'" + s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private void NothingSelected() =>
        MessageBox.Show(this, "Select an item in the list first.", "Lost & Found",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
}
