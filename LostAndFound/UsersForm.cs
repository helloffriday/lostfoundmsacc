using LostAndFound.Auth;
using LostAndFound.Models;

namespace LostAndFound;

/// <summary>Administrator screen: approve sign-ups, disable accounts, change roles, reset passwords.</summary>
public class UsersForm : Form
{
    private readonly AuthService _auth;
    private readonly DataGridView _grid = new();

    public UsersForm(AuthService auth)
    {
        _auth = auth;
        Text = "Manage Users";
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(820, 420);
        MinimumSize = new Size(700, 300);
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.MultiSelect = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.BackgroundColor = SystemColors.Window;
        foreach (var header in new[] { "Username", "Full name", "Role", "Status", "Last sign-in" })
            _grid.Columns.Add(header, header);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 8, 8, 4) };
        bar.Controls.Add(MakeButton("Approve / Enable", (_, _) => Act(u => _auth.SetActive(u, true))));
        bar.Controls.Add(MakeButton("Disable", (_, _) => Act(u => _auth.SetActive(u, false))));
        bar.Controls.Add(MakeButton("Make Admin", (_, _) => Act(u => _auth.SetRole(u, UserRole.Admin))));
        bar.Controls.Add(MakeButton("Make User", (_, _) => Act(u => _auth.SetRole(u, UserRole.User))));
        bar.Controls.Add(MakeButton("Reset Password", (_, _) => ResetPassword()));
        bar.Controls.Add(MakeButton("Delete", (_, _) => DeleteUser()));

        Controls.Add(_grid);
        Controls.Add(bar);
        RefreshGrid();
    }

    private static Button MakeButton(string text, EventHandler onClick)
    {
        var b = new Button { Text = text, AutoSize = true, Padding = new Padding(6, 2, 6, 2) };
        b.Click += onClick;
        return b;
    }

    private User? Selected => _grid.CurrentRow?.Tag as User;

    private void RefreshGrid()
    {
        var selectedId = Selected?.Id;
        _grid.Rows.Clear();
        foreach (var u in _auth.Users.OrderBy(u => u.Username, StringComparer.OrdinalIgnoreCase))
        {
            int idx = _grid.Rows.Add(
                u.Username, u.FullName, u.Role.ToString(),
                u.IsActive ? "Active" : "Pending / disabled",
                u.LastSignIn?.ToString("yyyy-MM-dd HH:mm") ?? "never");
            var row = _grid.Rows[idx];
            row.Tag = u;
            if (!u.IsActive) row.DefaultCellStyle.ForeColor = Color.Gray;
            if (selectedId == u.Id) _grid.CurrentCell = row.Cells[0];
        }
    }

    private void Act(Func<User, string?> action)
    {
        var user = Selected;
        if (user == null) { NothingSelected(); return; }
        var error = action(user);
        if (error != null)
            MessageBox.Show(this, error, "Manage Users", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        RefreshGrid();
    }

    private void ResetPassword()
    {
        var user = Selected;
        if (user == null) { NothingSelected(); return; }
        using var form = new ChangePasswordForm(_auth, user);
        form.ShowDialog(this);
    }

    private void DeleteUser()
    {
        var user = Selected;
        if (user == null) { NothingSelected(); return; }
        var answer = MessageBox.Show(this, $"Delete user \"{user.Username}\"? This cannot be undone.", "Confirm delete",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes) return;
        Act(u => _auth.DeleteUser(u));
    }

    private void NothingSelected() =>
        MessageBox.Show(this, "Select a user in the list first.", "Manage Users",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
}
