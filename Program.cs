using System.Text;
using IssuePad.Forms;
using IssuePad.Services;

namespace IssuePad;

internal static class Program
{
    private static string CrashLogPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IssuePad", "crash.log");

    [STAThread]
    static void Main()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => LogAndShow(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogAndShow(e.ExceptionObject as Exception ?? new Exception($"UnhandledException: {e.ExceptionObject}"));

        try
        {
            ApplicationConfiguration.Initialize();

            var repository = new IssueRepository();
            Application.Run(new MainForm(repository));
        }
        catch (Exception ex)
        {
            LogAndShow(ex);
        }
    }

    private static void LogAndShow(Exception ex)
    {
        try
        {
            File.AppendAllText(CrashLogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n",
                Encoding.UTF8);
        }
        catch { /* ignore */ }

        try
        {
            MessageBox.Show(ex.ToString(), "IssuePad - Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch { /* ignore */ }

        Environment.Exit(1);
    }
}
