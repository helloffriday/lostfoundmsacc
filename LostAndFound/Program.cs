using LostAndFound.Auth;
using LostAndFound.Data;

namespace LostAndFound;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        AuthService auth;
        try
        {
            auth = new AuthService(new UserStore());
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Could not open the database:\n\n" + ex.Message +
                "\n\nThis app stores its data in a Microsoft Access file and needs the 64-bit " +
                "\"Microsoft Access Database Engine\" (ACE OLE DB provider) installed - the same one inventory.exe uses.\n\n" +
                "Database file: " + Database.DbPath,
                "Lost & Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        string? prefill = null;

        // Workflow: (auto) sign in -> main window -> Logout returns here; closing the window exits.
        while (true)
        {
            if (auth.TryAutoSignIn() == null)
            {
                // First launch ever: create the administrator account before showing sign-in.
                if (!auth.HasUsers)
                {
                    using var setup = new RegisterForm(auth, firstRun: true);
                    if (setup.ShowDialog() != DialogResult.OK) return;
                    prefill = setup.RegisteredUsername;
                }

                using var signIn = new SignInForm(auth, prefill);
                if (signIn.ShowDialog() != DialogResult.OK) return;
            }

            var main = new MainForm(auth);
            Application.Run(main);
            bool loggedOut = main.LoggedOut;
            main.Dispose();
            if (!loggedOut) return;
            prefill = null;
        }
    }
}
