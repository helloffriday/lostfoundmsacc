using LostAndFound.Models;

namespace LostAndFound;

public class ItemForm : Form
{
    // One column: each label sits directly above its input, so nothing can misalign.
    private readonly TableLayoutPanel _layout = new()
    {
        Dock = DockStyle.Fill,
        ColumnCount = 1,
        AutoScroll = true,
        Padding = new Padding(12)
    };

    private readonly ComboBox _type = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _name = new() { MaxLength = 255 };
    private readonly ComboBox _category = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _description = new() { Multiline = true, Height = 70, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox _location = new() { MaxLength = 255 };
    private readonly DateTimePicker _date = new() { Format = DateTimePickerFormat.Short };
    private readonly TextBox _contactName = new() { MaxLength = 255 };
    private readonly TextBox _contactInfo = new() { MaxLength = 255 };
    private readonly ComboBox _status = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _notes = new() { Multiline = true, Height = 80, ScrollBars = ScrollBars.Vertical };

    public Item Item { get; }

    public ItemForm(Item? existing = null)
    {
        Item = existing ?? new Item();
        Text = existing == null ? "Add Item" : "Edit Item";

        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;

        ClientSize = new Size(480, 700);
        MinimumSize = new Size(420, 520);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _type.Items.AddRange(Enum.GetNames<ItemType>());
        _category.Items.AddRange(Item.Categories);
        _status.Items.AddRange(Enum.GetNames<ItemStatus>());

        AddField("Type", _type);
        AddField("Item name *", _name);
        AddField("Category", _category);
        AddField("Description", _description);
        AddField("Location", _location);
        AddField("Date", _date);
        AddField("Contact name", _contactName);
        AddField("Contact info", _contactInfo);
        AddField("Status", _status);
        AddField("Notes", _notes);

        var ok = new Button { Text = "Save", Width = 90, Height = 30 };
        var cancel = new Button { Text = "Cancel", Width = 90, Height = 30, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => SaveAndClose();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48,
            Padding = new Padding(8)
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);

        // Fill control first, then the bottom panel, so docking lays out correctly.
        Controls.Add(_layout);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;

        LoadFromItem();
    }

    /// <summary>Adds a label row followed by an input row.</summary>
    private void AddField(string label, Control input)
    {
        var lbl = new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Margin = new Padding(3, 6, 3, 2)
        };
        input.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        input.Margin = new Padding(3, 0, 3, 6);

        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.Controls.Add(lbl, 0, _layout.RowCount++);
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.Controls.Add(input, 0, _layout.RowCount++);
    }

    private void LoadFromItem()
    {
        _type.SelectedItem = Item.Type.ToString();
        _name.Text = Item.Name;
        _category.SelectedItem = Item.Category;
        if (_category.SelectedIndex < 0) _category.SelectedItem = "Other";
        _description.Text = Item.Description;
        _location.Text = Item.Location;
        _date.Value = Item.DateReported;
        _contactName.Text = Item.ContactName;
        _contactInfo.Text = Item.ContactInfo;
        _status.SelectedItem = Item.Status.ToString();
        _notes.Text = Item.Notes;
    }

    private void SaveAndClose()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
        {
            MessageBox.Show(this, "Please enter an item name.", "Missing information",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _name.Focus();
            return;
        }

        Item.Type = Enum.Parse<ItemType>((string)_type.SelectedItem!);
        Item.Name = _name.Text.Trim();
        Item.Category = (string)_category.SelectedItem!;
        Item.Description = _description.Text.Trim();
        Item.Location = _location.Text.Trim();
        Item.DateReported = _date.Value.Date;
        Item.ContactName = _contactName.Text.Trim();
        Item.ContactInfo = _contactInfo.Text.Trim();
        var newStatus = Enum.Parse<ItemStatus>((string)_status.SelectedItem!);
        if (newStatus != Item.Status)
            Item.ResolvedDate = newStatus == ItemStatus.Open ? null : DateTime.Today;
        Item.Status = newStatus;
        Item.Notes = _notes.Text.Trim();

        DialogResult = DialogResult.OK;
        Close();
    }
}
