using System.Globalization;

namespace ClickDesk;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => MessageBox.Show(e.Exception.Message,
            "ClickDesk error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        Application.Run(new MainForm(args.FirstOrDefault()));
    }
}
