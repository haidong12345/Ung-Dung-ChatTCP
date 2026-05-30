using System;

class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("server", StringComparison.OrdinalIgnoreCase))
        {
            Server.RunServer();
            return 0;
        }

        if (args.Length > 0 && args[0].Equals("client", StringComparison.OrdinalIgnoreCase))
        {
            Client.RunClient();
            return 0;
        }

        Console.WriteLine("Usage: dotnet run -- [server|client]");
        return 1;
    }
}
