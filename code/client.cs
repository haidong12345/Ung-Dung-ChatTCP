using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Client
{
    private static TcpClient? client;
    private static NetworkStream? stream;

    public static void RunClient()
    {
        try
        {
            client = new TcpClient("localhost", 5000);
            stream = client.GetStream();

            Thread receiveThread = new Thread(ReceiveMessages);
            receiveThread.Start();

            Console.WriteLine("Connected to server.");
            Console.WriteLine("Enter messages (type 'exit' to quit):");

            while (true)
            {
                string? message = Console.ReadLine();
                if (string.IsNullOrEmpty(message))
                    continue;

                if (message == "exit")
                    break;

                byte[] data = Encoding.UTF8.GetBytes(message);
                stream.Write(data, 0, data.Length);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            stream?.Close();
            client?.Close();
        }
    }

    private static void ReceiveMessages()
    {
        byte[] buffer = new byte[1024];
        int bytesRead;

        try
        {
            while (stream != null && (bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Server: {message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error receiving messages: {ex.Message}");
        }
    }
}