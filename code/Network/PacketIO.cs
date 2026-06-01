using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ChatApp.Models;

namespace ChatApp.Network;

public static class PacketIO
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static async Task SendAsync(NetworkStream stream, Packet packet, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(packet, Opts);
        var data = Encoding.UTF8.GetBytes(json);
        var len = BitConverter.GetBytes(data.Length);
        await stream.WriteAsync(len, ct);
        await stream.WriteAsync(data, ct);
    }

    public static async Task<Packet?> ReadAsync(NetworkStream stream, CancellationToken ct = default)
    {
        var lenBuf = new byte[4];
        if (!await ReadExactAsync(stream, lenBuf, ct)) return null;

        int length = BitConverter.ToInt32(lenBuf, 0);
        if (length <= 0 || length > 50_000_000) return null;

        var data = new byte[length];
        if (!await ReadExactAsync(stream, data, ct)) return null;

        return JsonSerializer.Deserialize<Packet>(Encoding.UTF8.GetString(data), Opts);
    }

    private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read), ct);
            if (n == 0) return false;
            read += n;
        }
        return true;
    }

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
