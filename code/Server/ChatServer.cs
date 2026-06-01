using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using ChatApp.Data;

namespace ChatApp.Server;

public static class ChatServer
{
    private const int Port = 5000;
    private static TcpListener? _listener;
    private static bool _running;

    // token -> userId
    internal static readonly Dictionary<string, string> Sessions = new();
    // userId -> ClientSession
    internal static readonly Dictionary<string, ClientSession> Online = new();
    private static readonly object Lock = new();

    public static void Run()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            AllocConsole();

        _listener = new TcpListener(IPAddress.Any, Port);
        _listener.Start();
        _running = true;

        Console.WriteLine($"=== Chat Server TCP - cong {Port} ===");
        Console.WriteLine("Admin mặc định: admin / admin123");
        Console.WriteLine("Nhan Enter để dừng server...");

        _ = Task.Run(AcceptLoop);
        Console.ReadLine();
        _running = false;
        _listener.Stop();
    }

    private static async Task AcceptLoop()
    {
        while (_running && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync();
                var session = new ClientSession(client);
                _ = session.RunAsync();
            }
            catch (Exception ex) when (_running)
            {
                Console.WriteLine($"Loi accept: {ex.Message}");
            }
        }
    }

    internal static void RegisterSession(string userId, ClientSession session)
    {
        lock (Lock)
        {
            if (Online.TryGetValue(userId, out var old))
                old.Dispose();
            Online[userId] = session;
        }
        BroadcastUserStatus(userId, true);
    }

    internal static void UnregisterSession(string userId)
    {
        lock (Lock)
        {
            Online.Remove(userId);
        }
        BroadcastUserStatus(userId, false);
    }

    internal static void BroadcastUserStatus(string userId, bool online)
    {
        var packet = new Models.Packet
        {
            Type = "USER_STATUS",
            Payload = new { userId, online },
        };
        Broadcast(packet, exceptUserId: null);
    }

    internal static void SendToUser(string userId, Models.Packet packet)
    {
        lock (Lock)
        {
            if (Online.TryGetValue(userId, out var s))
                _ = s.SendAsync(packet);
        }
    }

    internal static void Broadcast(Models.Packet packet, string? exceptUserId)
    {
        List<ClientSession> copy;
        lock (Lock) copy = Online.Values.ToList();

        foreach (var s in copy)
        {
            if (s.UserId == exceptUserId) continue;
            _ = s.SendAsync(packet);
        }
    }

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();
}
