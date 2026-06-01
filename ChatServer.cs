using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class ChatServer
{
    private TcpListener server;
    private const int PORT = 5000;

    public void Start()
    {
        server = new TcpListener(IPAddress.Any, PORT);
        server.Start();

        Console.WriteLine($"Server đang lắng nghe cổng {PORT}");

        while (true)
        {
            TcpClient client = server.AcceptTcpClient();

            Console.WriteLine("Client vừa kết nối");

            ClientHandler handler = new ClientHandler(client);

            Thread thread = new Thread(handler.Handle);
            thread.Start();
        }
    }
}