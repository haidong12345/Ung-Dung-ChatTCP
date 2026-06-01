using System;
using System.Net;
using System.Net.Sockets;

namespace ChatServer
{
    class Program
    {
        static void Main(string[] args)
        {
            TcpListener server = new TcpListener(IPAddress.Any, 8888);

            server.Start();

            Console.WriteLine("Server da khoi dong...");
            Console.WriteLine("Dang lang nghe cong 8888");

            while (true)
            {
                TcpClient client = server.AcceptTcpClient();

                Console.WriteLine("Co client moi ket noi");

                ClientHandler handler = new ClientHandler(client);

                Thread thread = new Thread(handler.Run);
                thread.Start();
            }
        }
    }
}