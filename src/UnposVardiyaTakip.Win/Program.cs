using UnposVardiyaTakip.Core;

namespace UnposVardiyaTakip.Win;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        EncodingBootstrap.Ensure();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.Run(new MainForm());
    }
}

internal static class EncodingBootstrap
{
    public static void Ensure() =>
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
}
