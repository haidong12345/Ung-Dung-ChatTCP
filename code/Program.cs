using ChatApp.Server;
using ChatApp.UI;

namespace ChatApp;

static class Program
{
    [STAThread]
    static void Main(string[] args)
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
