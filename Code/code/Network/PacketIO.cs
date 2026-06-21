using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ChatApp.Models;

namespace ChatApp.Network;

/// <summary>
/// Gửi/nhận gói tin qua TCP.
/// Quy ước: [4 byte độ dài JSON] + [chuỗi JSON của Packet]
/// TCP chỉ là luồng byte liên tục, nên phải có "khung" (framing) như trên.
/// </summary>
public static class PacketIO
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Chuyển Packet thành JSON rồi ghi xuống socket.</summary>
    public static async Task SendAsync(NetworkStream stream, Packet packet, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(packet, Opts);
        var data = Encoding.UTF8.GetBytes(json);

        // Ghi 4 byte đầu = số byte của phần JSON
        var len = BitConverter.GetBytes(data.Length);
        await stream.WriteAsync(len, ct);
        await stream.WriteAsync(data, ct);
    }

    /// <summary>Đọc 1 gói tin từ socket. Trả null nếu client đã ngắt kết nối.</summary>
    public static async Task<Packet?> ReadAsync(NetworkStream stream, CancellationToken ct = default)
    {
        // Bước 1: đọc 4 byte độ dài
        var lenBuf = new byte[4];
        if (!await ReadExactAsync(stream, lenBuf, ct)) return null;

        int length = BitConverter.ToInt32(lenBuf, 0);
        if (length <= 0 || length > 50_000_000) return null; // giới hạn an toàn ~50MB

        // Bước 2: đọc đủ số byte JSON
        var data = new byte[length];
        if (!await ReadExactAsync(stream, data, ct)) return null;

        return JsonSerializer.Deserialize<Packet>(Encoding.UTF8.GetString(data), Opts);
    }

    /// <summary>
    /// ReadAsync có thể trả về ít byte hơn yêu cầu.
    /// Hàm này đọc lặp cho đến khi đủ buffer.Length byte.
    /// </summary>
    private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read), ct);
            if (n == 0) return false; // đối phương đóng kết nối
            read += n;
        }
        return true;
    }

    /// <summary>Chuyển trường Payload (kiểu object?) sang class cụ thể, ví dụ AuthPayload.</summary>
    public static T? ParsePayload<T>(object? payload) where T : class
    {
        if (payload is null) return null;
        var json = payload is JsonElement el ? el.GetRawText() : JsonSerializer.Serialize(payload);
        return JsonSerializer.Deserialize<T>(json, Opts);
    }

    public static List<UserInfo> ParseUserList(object? payload)
    {
        if (payload is null) return new();
        var json = payload is JsonElement el ? el.GetRawText() : JsonSerializer.Serialize(payload);
        return JsonSerializer.Deserialize<List<UserInfo>>(json, Opts) ?? new();
    }
}
