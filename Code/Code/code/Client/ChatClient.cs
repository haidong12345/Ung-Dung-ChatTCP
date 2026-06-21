using System.Net.Sockets;
using ChatApp.Models;
using ChatApp.Network;

namespace ChatApp.Client;

/// <summary>
/// Lớp kết nối TCP phía client.
/// - Luồng đọc nền: nhận mọi gói tin từ server và bắn sự kiện OnPacket
/// - SendAndWaitAsync: gửi 1 lệnh và chờ đúng gói trả lời (LOGIN_OK, SEND_MESSAGE_OK, ...)
/// </summary>
public class ChatClient : IDisposable
{
    private readonly SemaphoreSlim _sendLock = new(1, 1); // chỉ gửi 1 gói tại 1 thời điểm
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private Task? _readTask;

    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5000;
    public string? Token { get; private set; }
    public UserInfo? CurrentUser { get; private set; }
    public bool Connected => _tcp?.Connected == true;

    /// <summary>Sự kiện này chạy khi server gửi bất kỳ gói tin nào (tin mới, typing, ...).</summary>
    public event Action<Packet>? OnPacket;

    public async Task ConnectAsync()
    {
        _tcp = new TcpClient();
        await _tcp.ConnectAsync(Host, Port);
        _stream = _tcp.GetStream();
        _cts = new CancellationTokenSource();

        // Tạo luồng nền đọc liên tục từ server
        _readTask = Task.Run(() => ReadLoop(_cts.Token));
    }

    private async Task ReadLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _stream != null)
        {
            var packet = await PacketIO.ReadAsync(_stream, ct);
            if (packet is null) break;
            OnPacket?.Invoke(packet);
        }
    }

    /// <summary>
    /// Gửi request và chờ response tương ứng (ví dụ gửi LOGIN, chờ LOGIN_OK).
    /// Cách làm: tạm đăng ký handler OnPacket, khi nhận đúng loại gói thì trả kết quả.
    /// </summary>
    public async Task<Packet> SendAndWaitAsync(Packet request, string okType, int timeoutMs = 15000)
    {
        var tcs = new TaskCompletionSource<Packet>();
        var expectedOk = okType.ToUpperInvariant();

        void Handler(Packet p)
        {
            var t = (p.Type ?? "").ToUpperInvariant();
            if (t == expectedOk || t == "ERROR" || t == request.Type.ToUpperInvariant() + "_OK")
                tcs.TrySetResult(p);
        }

        OnPacket += Handler;
        try
        {
            await SendAsync(request);

            // Chờ phản hồi hoặc hết thời gian
            var done = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
            if (done != tcs.Task)
                return new Packet { Type = okType, Ok = false, Error = "Hết thời gian chờ" };
            return await tcs.Task;
        }
        finally
        {
            OnPacket -= Handler; // luôn gỡ handler để tránh rò rỉ bộ nhớ
        }
    }

    public async Task SendAsync(Packet packet)
    {
        if (_stream is null) throw new InvalidOperationException("Chưa kết nối server");
        await _sendLock.WaitAsync();
        try
        {
            await PacketIO.SendAsync(_stream, packet);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void SetAuth(AuthResult auth)
    {
        Token = auth.Token;
        CurrentUser = auth.User;
    }

    public void ClearAuth()
    {
        Token = null;
        CurrentUser = null;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _stream?.Close();
        _tcp?.Close();
    }
}