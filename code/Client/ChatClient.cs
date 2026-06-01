using System.Net.Sockets;
using ChatApp.Models;
using ChatApp.Network;

namespace ChatApp.Client;

/// <summary>Kết nối TCP tới server, gửi/nhận Packet</summary>
public class ChatClient : IDisposable
{
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private Task? _readTask;

    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5000;
    public string? Token { get; private set; }
    public UserInfo? CurrentUser { get; private set; }
    public bool Connected => _tcp?.Connected == true;

    public event Action<Packet>? OnPacket;

    public async Task ConnectAsync()
    {
        _tcp = new TcpClient();
        await _tcp.ConnectAsync(Host, Port);
        _stream = _tcp.GetStream();
        _cts = new CancellationTokenSource();
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

    public async Task<Packet> SendAndWaitAsync(Packet request, string okType, int timeoutMs = 10000)
    {
        var tcs = new TaskCompletionSource<Packet>();
        void Handler(Packet p)
        {
            if (p.Type == okType || p.Type == "ERROR" || (p.Type == request.Type + "_OK"))
            {
                OnPacket -= Handler;
                tcs.TrySetResult(p);
            }
        }
        OnPacket += Handler;
        await SendAsync(request);
        var done = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
        OnPacket -= Handler;
        if (done != tcs.Task)
            return new Packet { Type = okType, Ok = false, Error = "Hết thời gian chờ" };
        return await tcs.Task;
    }

    public async Task SendAsync(Packet packet)
    {
        if (_stream is null) throw new InvalidOperationException("Chưa kết nối");
        await PacketIO.SendAsync(_stream, packet);
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
