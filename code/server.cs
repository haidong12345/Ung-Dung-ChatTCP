using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Server
{
    private static TcpListener? listener;
    private static bool isRunning = false;

    public static void RunServer()
    {
        int port = 5000; // You can change this to any port you want
        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        isRunning = true;
        Console.WriteLine($"Server started on port {port}.");

        while (isRunning)
        {
            try
            {
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine("Client connected.");
                Thread clientThread = new Thread(HandleClient);
                clientThread.Start(client);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting client: {ex.Message}");
            }
        }
    }

    private static void HandleClient(object? obj)
    {
        if (obj is not TcpClient client)
            return;

        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[1024];
        int bytesRead;

        try
        {
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Received: {message}");
                // Echo the message back to the client
                byte[] response = Encoding.UTF8.GetBytes($"Echo: {message}");
                stream.Write(response, 0, response.Length);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client: {ex.Message}");
        }
        finally
        {
            stream.Close();
            client.Close();
            Console.WriteLine("Client disconnected.");
        }
    }
}