using ChatApp.Server;
using ChatApp.UI;

namespace ChatApp;

/// <summary>
/// Điểm bắt đầu của chương trình.
/// - Chạy "dotnet run -- server"  => mở SERVER (console, lắng nghe cổng 5000)
/// - Chạy "dotnet run"            => mở CLIENT (giao diện đăng nhập)
/// </summary>
static class Program
{
    [STAThread] // WinForms bắt buộc dùng chế độ STA (Single Thread Apartment)
    static void Main(string[] args)
    {
        // Tham số dòng lệnh: nếu gõ "server" thì chạy phía server
        if (args.Length > 0 && args[0].Equals("server", StringComparison.OrdinalIgnoreCase))
        {
            ChatServer.Run();
            return;
        }

        // Ngược lại: mở giao diện client
        ApplicationConfiguration.Initialize();
        Application.Run(new LoginForm());
    }
}
