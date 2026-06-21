using ChatApp.Server;
using ChatApp.UI;

namespace ChatApp;

/// <summary>
/// Điểm khởi đầu của ứng dụng.
/// - Chạy với tham số "server" (ví dụ: MyApp.exe server) -> khởi động ChatServer (console).
/// - Chạy không tham số -> mở giao diện WinForms (LoginForm) cho client.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("server", StringComparison.OrdinalIgnoreCase))
        {
            ChatServer.Run();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new LoginForm());
    }
}