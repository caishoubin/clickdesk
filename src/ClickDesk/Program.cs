namespace ClickDesk;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => MessageBox.Show(e.Exception.Message,
            "ClickDesk 遇到问题", MessageBoxButtons.OK, MessageBoxIcon.Error);
        Application.Run(new MainForm(args.FirstOrDefault()));
    }
}
