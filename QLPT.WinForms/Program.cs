using QLPT.WinForms.Forms;

namespace QLPT.WinForms;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => MessageBox.Show(
            "Không thực hiện được thao tác. " + e.Exception.Message,
            "Lỗi ứng dụng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (var login = new LoginForm())
        {
            if (login.ShowDialog() != DialogResult.OK)
                return;
        }
        Application.Run(new MainForm());
    }
}
