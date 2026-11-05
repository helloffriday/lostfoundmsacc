using LostAndFound.Auth;

namespace LostAndFound;

public class RegisterForm : Form
{
    private readonly AuthService _auth;
    private readonly TextBox _fullName = new() { MaxLength = 100 };
    private readonly TextBox _username = new() { MaxLength = 30 };
    private readonly TextBox _password = new() { UseSystemPasswordChar = true };
    private readonly TextBox _confirm = new() { UseSystemPasswordChar = true };
    private readonly Label _error = new()
    {
        AutoSize = true,
        ForeColor = Color.Firebrick,
        MaximumSize = new Size(340, 0),
        Margin = new Padding(3, 4, 3, 4)
    };

    public string RegisteredUsername { get; private set; } = "";

    public RegisterForm(AuthService auth, bool firstRun)
    {
        _auth = auth;
        Text = firstRun ? "Lost & Found - First-time Setup" : "Lost & Found - Register";
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(400, 500);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(24, 20, 24, 12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label
        {
            Text = firstRun ? "Create the administrator account" : "Create your account",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 13F, FontStyle.Bold),
            Margin = new Padding(3, 0, 3, 2)
        });
        layout.Controls.Add(new Label
        {
            Text = firstRun
                ? "This is the first account, so it will have full administrator access."
                : "An administrator must approve your account before you can sign in.",
            AutoSize = true,
            MaximumSize = new Size(340, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 0, 3, 12)
        });

        AddField(layout, "Full name", _fullName);
        AddField(layout, "Username", _username);
        AddField(layout, $"Password (min {AuthService.MinPasswordLength} characters)", _password);
        AddField(layout, "Re-enter password", _confirm);
        layout.Controls.Add(_error);

        var create = new Button { Text = "Create Account", Width = 130, Height = 32 };
        var cancel = new Button { Text = "Cancel", Width = 90, Height = 32, DialogResult = DialogResult.Cancel };
        create.Click += (_, _) => DoRegister();
        var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        buttons.Controls.Add(create);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons);

        Controls.Add(layout);
        AcceptButton = create;
        CancelButton = cancel;
    }

    private static void AddField(TableLayoutPanel layout, string label, Control input)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 6, 3, 2) });
        input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        input.Margin = new Padding(3, 0, 3, 6);
        layout.Controls.Add(input);
    }

    private void DoRegister()
    {
        var (ok, message, user) = _auth.Register(_fullName.Text, _username.Text, _password.Text, _confirm.Text);
        if (!ok)
        {
            _error.Text = message;
            return;
        }
        MessageBox.Show(this, message, "Lost & Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
        RegisteredUsername = user!.Username;
        DialogResult = DialogResult.OK;
        Close();
    }
}
