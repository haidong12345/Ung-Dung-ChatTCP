using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using ChatApp.Data;

namespace ChatApp.Server;

/// <summary>
/// Server TCP trung tâm.
/// - Lắng nghe cổng 5000, mỗi client kết nối tạo 1 ClientSession
/// - Quản lý ai đang online và chuyển tin nhắn realtime giữa các user
/// </summary>
public static class ChatServer
{
    private const int Port = 5000;
    private static TcpListener? _listener;
    private static bool _running;

    // Bảng tra cứu sau khi đăng nhập:
    internal static readonly Dictionary<string, string> Sessions = new(); // token -> userId
    internal static readonly Dictionary<string, ClientSession> Online = new(); // userId -> session đang kết nối
    private static readonly object Lock = new();

    public static void Run()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            AllocConsole(); // mở cửa sổ console trên Windows

        _listener = new TcpListener(IPAddress.Any, Port);
        _listener.Start();
        _running = true;

        Console.WriteLine($"=== Chat Server TCP - cong {Port} ===");
        Console.WriteLine("Admin mac dinh: admin / admin123");
        Console.WriteLine("Nhan Enter de dung server...");

        _ = Task.Run(AcceptLoop);
        Console.ReadLine();
        _running = false;
        _listener.Stop();
    }

    /// <summary>Vòng lặp chờ client mới kết nối.</summary>
    private static async Task AcceptLoop()
    {
        while (_running && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync();
                var session = new ClientSession(client);
                _ = session.RunAsync(); // xử lý client trên luồng riêng
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
            // Nếu user đăng nhập lại từ máy khác, đóng session cũ
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

    /// <summary>Gửi gói tin tới đúng 1 user (nếu user đó đang online).</summary>
    internal static void SendToUser(string userId, Models.Packet packet)
    {
lock (Lock)
        {
            if (Online.TryGetValue(userId, out var s))
                _ = s.SendAsync(packet);
        }
    }

    /// <summary>Gửi gói tin tới tất cả user online (trừ exceptUserId nếu có).</summary>
    internal static void Broadcast(Models.Packet packet, string? exceptUserId)
    {
        List<ClientSession> copy;
        lock (Lock) copy = Online.Values.ToList(); // copy ra ngoài lock để tránh treo lâu

        foreach (var s in copy)
        {
            if (s.UserId == exceptUserId) continue;
            _ = s.SendAsync(packet);
        }
    }

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();
}
