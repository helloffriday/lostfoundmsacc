using LostAndFound.Auth;
using LostAndFound.Models;

namespace LostAndFound;

/// <summary>
/// Change your own password (asks for the current one), or - for an administrator -
/// reset another user's password by passing that user as <paramref name="target"/>.
/// </summary>
public class ChangePasswordForm : Form
{
    private readonly AuthService _auth;
    private readonly User? _target;
    private readonly TextBox _current = new() { UseSystemPasswordChar = true };
    private readonly TextBox _new = new() { UseSystemPasswordChar = true };
    private readonly TextBox _confirm = new() { UseSystemPasswordChar = true };
    private readonly Label _error = new()
    {
        AutoSize = true,
        ForeColor = Color.Firebrick,
        MaximumSize = new Size(320, 0),
        Margin = new Padding(3, 4, 3, 4)
    };

    public ChangePasswordForm(AuthService auth, User? target = null)
    {
        _auth = auth;
        _target = target;
        Text = target == null ? "Change Password" : $"Reset Password - {target.Username}";
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(380, target == null ? 380 : 310);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(20, 16, 20, 12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        if (target == null) AddField(layout, "Current password", _current);
        AddField(layout, $"New password (min {AuthService.MinPasswordLength} characters)", _new);
        AddField(layout, "Re-enter new password", _confirm);
        layout.Controls.Add(_error);

        var save = new Button { Text = "Save", Width = 90, Height = 32 };
        var cancel = new Button { Text = "Cancel", Width = 90, Height = 32, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save();
        var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons);

        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private static void AddField(TableLayoutPanel layout, string label, Control input)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 6, 3, 2) });
        input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        input.Margin = new Padding(3, 0, 3, 6);
        layout.Controls.Add(input);
    }

    private void Save()
    {
        if (_new.Text != _confirm.Text)
        {
            _error.Text = "Passwords do not match.";
            return;
        }

        var error = _target == null
            ? _auth.ChangeOwnPassword(_current.Text, _new.Text)
            : _auth.ResetPassword(_target, _new.Text);
        if (error != null)
        {
            _error.Text = error;
            return;
        }

        MessageBox.Show(this, "Password updated.", "Lost & Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }
}
