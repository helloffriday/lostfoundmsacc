using LostAndFound.Auth;

namespace LostAndFound;

public class SignInForm : Form
{
    private readonly AuthService _auth;
    private readonly TextBox _username = new();
    private readonly TextBox _password = new() { UseSystemPasswordChar = true };
    private readonly CheckBox _show = new() { Text = "Show password", AutoSize = true };
    private readonly CheckBox _remember = new() { Text = "Keep me signed in", AutoSize = true };
    private readonly Label _error = new()
    {
        AutoSize = true,
        ForeColor = Color.Firebrick,
        MaximumSize = new Size(340, 0),
        Margin = new Padding(3, 4, 3, 4)
    };

    public SignInForm(AuthService auth, string? prefillUsername = null)
    {
        _auth = auth;
        Text = "Lost & Found - Sign In";
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(400, 430);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(24, 20, 24, 12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label
        {
            Text = "LOST & FOUND",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
            Margin = new Padding(3, 0, 3, 0)
        });
        layout.Controls.Add(new Label
        {
            Text = "Sign in to your account",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 0, 3, 14)
        });

        AddField(layout, "Username", _username);
        AddField(layout, "Password", _password);

        _show.CheckedChanged += (_, _) => _password.UseSystemPasswordChar = !_show.Checked;
        layout.Controls.Add(_show);
        layout.Controls.Add(_remember);
        layout.Controls.Add(_error);

        var signIn = new Button { Text = "Sign In", Width = 110, Height = 32 };
        var exit = new Button { Text = "Exit", Width = 90, Height = 32, DialogResult = DialogResult.Cancel };
        signIn.Click += (_, _) => DoSignIn();
        var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        buttons.Controls.Add(signIn);
        buttons.Controls.Add(exit);
        layout.Controls.Add(buttons);

        var register = new LinkLabel { Text = "Don't have an account? Register here.", AutoSize = true, Margin = new Padding(3, 10, 3, 0) };
        register.LinkClicked += (_, _) => OpenRegister();
        layout.Controls.Add(register);

        Controls.Add(layout);
        AcceptButton = signIn;
        CancelButton = exit;

        if (!string.IsNullOrEmpty(prefillUsername))
        {
            _username.Text = prefillUsername;
            Shown += (_, _) => _password.Focus();
        }
    }

    private static void AddField(TableLayoutPanel layout, string label, Control input)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 6, 3, 2) });
        input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        input.Margin = new Padding(3, 0, 3, 6);
        layout.Controls.Add(input);
    }

    private void DoSignIn()
    {
        var (user, error) = _auth.SignIn(_username.Text, _password.Text, _remember.Checked);
        if (user == null)
        {
            _error.Text = error ?? "Sign in failed.";
            _password.Clear();
            _password.Focus();
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }

    private void OpenRegister()
    {
        using var form = new RegisterForm(_auth, firstRun: !_auth.HasUsers);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        _username.Text = form.RegisteredUsername;
        _password.Clear();
        _error.Text = "";
        _password.Focus();
    }
}
