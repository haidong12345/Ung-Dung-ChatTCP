using System.Net.Sockets;
using ChatApp.Data;
using ChatApp.Models;
using ChatApp.Network;

namespace ChatApp.Server;

/// <summary>Xử lý 1 kết nối TCP từ client</summary>
public class ClientSession : IDisposable
{
    private readonly TcpClient _tcp;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    public string? UserId { get; private set; }
    public string? Token { get; private set; }

    public ClientSession(TcpClient tcp) => _tcp = tcp;

    public async Task RunAsync()
    {
        _stream = _tcp.GetStream();
        _cts = new CancellationTokenSource();

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var packet = await PacketIO.ReadAsync(_stream, _cts.Token);
                if (packet is null) break;
                await HandleAsync(packet);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client ngắt kết nối: {ex.Message}");
        }
        finally
        {
            if (UserId != null)
            {
                ChatServer.Sessions.Remove(Token ?? "");
                ChatServer.UnregisterSession(UserId);
            }
            Dispose();
        }
    }

    public async Task SendAsync(Packet packet)
    {
        if (_stream is null) return;
        try { await PacketIO.SendAsync(_stream, packet, _cts?.Token ?? default); }
        catch { /* client đã đóng */ }
    }

    private async Task ReplyAsync(Packet req, object? payload, bool ok = true, string? error = null)
    {
        await SendAsync(new Packet
        {
            Type = req.Type + "_OK",
            Ok = ok,
            Error = error,
            Payload = payload,
        });
    }

    private async Task HandleAsync(Packet packet)
    {
        var cmd = (packet.Type ?? "").Trim().ToUpperInvariant();
        switch (cmd)
        {
            case "REGISTER": await HandleRegister(packet); break;
            case "LOGIN": await HandleLogin(packet); break;
            case "LOGOUT": await HandleLogout(packet); break;
            case "CHANGE_PASSWORD": await HandleChangePassword(packet); break;
            case "FORGOT_PASSWORD": await HandleForgot(packet); break;
            case "RESET_PASSWORD": await HandleReset(packet); break;
            case "GET_USERS": await HandleGetUsers(packet); break;
            case "GET_HISTORY": await HandleGetHistory(packet); break;
            case "SEND_MESSAGE": await HandleSendMessage(packet); break;
            case "RECALL_MESSAGE": await HandleRecall(packet); break;
            case "TYPING": await HandleTyping(packet); break;
            case "SEEN": await HandleSeen(packet); break;
            case "UPLOAD": await HandleUpload(packet); break;
            case "UPDATE_PROFILE": await HandleProfile(packet); break;
            case "ADMIN_USERS": await HandleAdminUsers(packet); break;
            case "ADMIN_LOCK": await HandleAdminLock(packet); break;
            case "GET_FILE": await HandleGetFile(packet); break;
            case "EMOJI_REPLY": await HandleEmojiReply(packet); break;
            default:
                Console.WriteLine($"Lệnh không hợp lệ: '{packet.Type}'");
                await SendAsync(new Packet { Type = "ERROR", Ok = false, Error = $"Lenh khong hop le: {packet.Type}" });
                break;
        }
    }

    private UserAccount? GetUserFromToken(Packet p)
    {
        var auth = PacketIO.ParsePayload<Dictionary<string, string>>(p.Payload);
        // token gửi kèm mọi yêu cầu sau login
        string? token = auth?.GetValueOrDefault("token");
        if (string.IsNullOrEmpty(token) && p.Payload is System.Text.Json.JsonElement el
            && el.TryGetProperty("token", out var t))
            token = t.GetString();

        if (token == null || !ChatServer.Sessions.TryGetValue(token, out var uid)) return null;
        return DataStore.LoadUsers().FirstOrDefault(u => u.Id == uid);
    }

    private string? GetToken(Packet p)
    {
        if (p.Payload is System.Text.Json.JsonElement el && el.TryGetProperty("token", out var t))
            return t.GetString();
        var dict = PacketIO.ParsePayload<Dictionary<string, string>>(p.Payload);
        return dict?.GetValueOrDefault("token");
    }

    private async Task HandleRegister(Packet p)
    {
        var data = PacketIO.ParsePayload<AuthPayload>(p.Payload);
        if (data is null || string.IsNullOrWhiteSpace(data.Username))
        {
            await ReplyAsync(p, null, false, "Nhập username và password");
            return;
        }

        var users = DataStore.LoadUsers();
        if (users.Any(u => u.Username == data.Username))
        {
            await ReplyAsync(p, null, false, "Username đã tồn tại");
            return;
        }

        var user = new UserAccount
        {
            Id = DataStore.NewId(),
            Username = data.Username,
            PasswordHash = DataStore.HashPassword(data.Password),
            DisplayName = data.DisplayName ?? data.Username,
            Role = "user",
            Status = "active",
        };
        users.Add(user);
        DataStore.SaveUsers(users);
        await LoginSuccess(p, user);
    }

    private async Task HandleLogin(Packet p)
    {
        var data = PacketIO.ParsePayload<AuthPayload>(p.Payload);
        if (data is null)
        {
            await ReplyAsync(p, null, false, "Thiếu thông tin");
            return;
        }

        var user = DataStore.LoadUsers().FirstOrDefault(u =>
            u.Username == data.Username && u.PasswordHash == DataStore.HashPassword(data.Password));

        if (user is null)
        {
            await ReplyAsync(p, null, false, "Sai tài khoản hoặc mật khẩu");
            return;
        }
        if (user.Status == "locked")
        {
            await ReplyAsync(p, null, false, "Tài khoản đã bị khóa");
            return;
        }
        await LoginSuccess(p, user);
    }

    private async Task LoginSuccess(Packet p, UserAccount user)
    {
        Token = Guid.NewGuid().ToString("N");
        UserId = user.Id;
        ChatServer.Sessions[Token] = user.Id;
        ChatServer.RegisterSession(user.Id, this);

        var others = DataStore.LoadUsers()
            .Where(u => u.Id != user.Id)
            .Select(u => UserInfo.From(u, ChatServer.Online.ContainsKey(u.Id)))
            .ToList();

        await ReplyAsync(p, new AuthResult
        {
            Token = Token,
            User = UserInfo.From(user, online: true),
            Users = others,
        });
    }

    private async Task HandleLogout(Packet p)
    {
        var token = GetToken(p);
        if (token != null) ChatServer.Sessions.Remove(token);
        if (UserId != null) ChatServer.UnregisterSession(UserId);
        UserId = null;
        await ReplyAsync(p, new { ok = true });
        _cts?.Cancel();
    }

    private async Task HandleChangePassword(Packet p)
    {
        var user = RequireUser(p);
        if (user is null) return;

        var data = PacketIO.ParsePayload<ChangePasswordPayload>(p.Payload);
        if (data is null || string.IsNullOrEmpty(data.NewPassword))
        {
            await ReplyAsync(p, null, false, "Nhập mật khẩu mới");
            return;
        }

        var users = DataStore.LoadUsers();
        var idx = users.FindIndex(u => u.Id == user.Id);
        if (users[idx].PasswordHash != DataStore.HashPassword(data.OldPassword))
        {
            await ReplyAsync(p, null, false, "Mật khẩu cũ không đúng");
            return;
        }
        users[idx].PasswordHash = DataStore.HashPassword(data.NewPassword);
        DataStore.SaveUsers(users);
        await ReplyAsync(p, new { ok = true });
    }

    private async Task HandleForgot(Packet p)
    {
        var data = PacketIO.ParsePayload<ForgotPayload>(p.Payload);
        var users = DataStore.LoadUsers();
        var idx = users.FindIndex(u => u.Username == data?.Username);
        if (idx < 0)
        {
            await ReplyAsync(p, null, false, "Không tìm thấy user");
            return;
        }
        var code = Random.Shared.Next(100000, 999999).ToString();
        users[idx].ResetCode = code;
        DataStore.SaveUsers(users);
        // Demo: trả mã về client (thực tế gửi email) (thực tế gửi email)
        await ReplyAsync(p, new { resetCode = code, message = "Mã đặt lại (demo):" });
    }

    private async Task HandleReset(Packet p)
    {
        var data = PacketIO.ParsePayload<ForgotPayload>(p.Payload);
        var users = DataStore.LoadUsers();
        var idx = users.FindIndex(u => u.Username == data?.Username);
        if (idx < 0 || users[idx].ResetCode != data?.ResetCode)
        {
            await ReplyAsync(p, null, false, "Mã không đúng");
            return;
        }
        users[idx].PasswordHash = DataStore.HashPassword(data!.NewPassword ?? "");
        users[idx].ResetCode = null;
        DataStore.SaveUsers(users);
        await ReplyAsync(p, new { ok = true });
    }

    private async Task HandleGetUsers(Packet p)
    {
        var user = RequireUser(p);
        if (user is null) return;

        // Hiển thị tất cả user khác (trừ bản thân) để chat 1-1
        var users = DataStore.LoadUsers()
            .Where(u => u.Id != user.Id)
            .Select(u => UserInfo.From(u, ChatServer.Online.ContainsKey(u.Id)))
            .ToList();
        await ReplyAsync(p, users);
    }

    private async Task HandleGetHistory(Packet p)
    {
        var user = RequireUser(p);
        if (user is null) return;

        var hist = PacketIO.ParsePayload<HistoryPayload>(p.Payload);
        var otherId = hist?.OtherUserId;

        var messages = DataStore.LoadMessages()
            .Where(m =>
                (m.FromUserId == user.Id && m.ToUserId == otherId) ||
                (m.FromUserId == otherId && m.ToUserId == user.Id))
            .OrderBy(m => m.CreatedAt)
            .ToList();
        await ReplyAsync(p, messages);
    }

    private async Task HandleSendMessage(Packet p)
    {
        var user = RequireUser(p);
        if (user is null) return;

        var data = PacketIO.ParsePayload<SendMessagePayload>(p.Payload);
        if (data is null || string.IsNullOrEmpty(data.ToUserId))
        {
            await ReplyAsync(p, null, false, "Thiếu người nhận");
            return;
        }

        var msg = new ChatMessage
        {
            Id = DataStore.NewId(),
            FromUserId = user.Id,
            ToUserId = data.ToUserId,
            Type = data.MessageType,
            Content = data.Content,
            FilePath = data.FilePath,
            FileName = data.FileName,
            SeenBy = new List<string> { user.Id },
            ReplyToId = data.ReplyToId ?? "",
        };

        if (!string.IsNullOrEmpty(msg.ReplyToId))
        {
            var allForQuote = DataStore.LoadMessages();
            var quoted = allForQuote.FirstOrDefault(m => m.Id == msg.ReplyToId);
            if (quoted != null)
                msg.ReplyPreview = BuildReplyPreview(quoted);
        }

        var all = DataStore.LoadMessages();
        all.Add(msg);
        DataStore.SaveMessages(all);

        var push = new Packet { Type = "NEW_MESSAGE", Payload = msg };
        ChatServer.SendToUser(data.ToUserId, push);
        await ReplyAsync(p, msg);
    }

    private async Task HandleRecall(Packet p)
    {
        var user = RequireUser(p);
        if (user is null) return;

        var data = PacketIO.ParsePayload<RecallPayload>(p.Payload);
        if (data is null) return;

        var all = DataStore.LoadMessages();
        var idx = all.FindIndex(m => m.Id == data.MessageId && m.FromUserId == user.Id);
        if (idx < 0) return;

        all[idx].Recalled = true;
        all[idx].Content = "";
        DataStore.SaveMessages(all);

        var push = new Packet { Type = "MESSAGE_RECALLED", Payload = new { data.MessageId, from = user.Id } };
        ChatServer.SendToUser(data.ToUserId, push);
        await ReplyAsync(p, new { ok = true });
    }

    private async Task HandleTyping(Packet p)
    {
        var user = RequireUser(p);
        if (user is null) return;

        var data = PacketIO.ParsePayload<TypingPayload>(p.Payload);
        if (data is null) return;

        ChatServer.SendToUser(data.ToUserId, new Packet
        {
            Type = "TYPING",
            Payload = new { fromUserId = user.Id, data.IsTyping },
        });
        await ReplyAsync(p, new { ok = true });
    }

    private async Task HandleSeen(Packet p)
    {
        var user = RequireUser(p);
        if (user is null) return;

        var data = PacketIO.ParsePayload<SeenPayload>(p.Payload);
        if (data is null) return;

        var all = DataStore.LoadMessages();
        foreach (var mid in data.MessageIds)
        {
            var m = all.FirstOrDefault(x => x.Id == mid);
            if (m != null && !m.SeenBy.Contains(user.Id))
                m.SeenBy.Add(user.Id);
        }
        DataStore.SaveMessages(all);

        ChatServer.SendToUser(data.ToUserId, new Packet
        {
            Type = "MESSAGES_SEEN",
            Payload = new { messageIds = data.MessageIds, byUserId = user.Id },
        });
        await ReplyAsync(p, new { ok = true });
    }

    private async Task HandleUpload(Packet p)
    {
        var user = RequireUser(p);
        if (user is null) return;

        var data = PacketIO.ParsePayload<UploadPayload>(p.Payload);
        if (data is null || string.IsNullOrEmpty(data.Base64Data))
        {
            await ReplyAsync(p, null, false, "Thiếu file");
            return;
        }

        var bytes = Convert.FromBase64String(data.Base64Data);
        var saved = DataStore.SaveUpload(data.FileName, bytes);
        var ext = Path.GetExtension(data.FileName).ToLowerInvariant();
        var type = ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" ? "image"
            : ext is ".mp4" or ".webm" or ".avi" or ".mov" or ".mkv" ? "video"
            : "file";

        await ReplyAsync(p, new UploadResult { FilePath = saved, Type = type });
    }

    private async Task HandleProfile(Packet p)
    {
        var user = RequireUser(p);
        if (user is null) return;

        var data = PacketIO.ParsePayload<ProfilePayload>(p.Payload);
        var users = DataStore.LoadUsers();
        var idx = users.FindIndex(u => u.Id == user.Id);

        if (!string.IsNullOrEmpty(data?.DisplayName))
            users[idx].DisplayName = data.DisplayName;

        if (!string.IsNullOrEmpty(data?.AvatarBase64))
        {
            var bytes = Convert.FromBase64String(data.AvatarBase64);
            var name = data.AvatarFileName ?? "avatar.png";
            users[idx].AvatarPath = DataStore.SaveUpload(name, bytes);
        }

        DataStore.SaveUsers(users);

        var updated = UserInfo.From(users[idx], ChatServer.Online.ContainsKey(users[idx].Id));
        ChatServer.Broadcast(new Packet { Type = "PROFILE_UPDATED", Payload = updated }, exceptUserId: null);
        await ReplyAsync(p, updated);
    }

    private static string BuildReplyPreview(ChatMessage m)
    {
        if (m.Recalled) return "[Tin đã thu hồi]";
        return m.Type switch
        {
            "image" => "[Ảnh] " + m.FileName,
            "video" => "[Video] " + m.FileName,
            "file" => "[File] " + m.FileName,
            _ => m.Content.Length > 80 ? m.Content[..80] + "…" : m.Content,
        };
    }

    private async Task HandleEmojiReply(Packet p)
    {
        var user = RequireUser(p);
        if (user is null) return;

        var data = PacketIO.ParsePayload<EmojiReplyPayload>(p.Payload);
        if (data is null || string.IsNullOrEmpty(data.MessageId) || string.IsNullOrEmpty(data.Emoji))
        {
            await ReplyAsync(p, null, false, "Thiếu thông tin");
            return;
        }

        var all = DataStore.LoadMessages();
        var idx = all.FindIndex(m => m.Id == data.MessageId);
        if (idx < 0)
        {
            await ReplyAsync(p, null, false, "Không tìm thấy tin");
            return;
        }

        var msg = all[idx];
        if (msg.FromUserId != user.Id && msg.ToUserId != user.Id)
        {
            await ReplyAsync(p, null, false, "Không có quyền");
            return;
        }

        var otherId = msg.FromUserId == user.Id ? msg.ToUserId : msg.FromUserId;
        var existing = msg.Reactions.FirstOrDefault(r => r.UserId == user.Id && r.Emoji == data.Emoji);
        if (existing != null)
            msg.Reactions.Remove(existing);
        else
            msg.Reactions.Add(new MessageReaction { UserId = user.Id, Emoji = data.Emoji });

        DataStore.SaveMessages(all);

        var push = new Packet
        {
            Type = "MESSAGE_REACTION",
            Payload = new { messageId = data.MessageId, reactions = msg.Reactions },
        };
        ChatServer.SendToUser(otherId, push);
        await ReplyAsync(p, new { messageId = data.MessageId, reactions = msg.Reactions });
    }

    private async Task HandleAdminUsers(Packet p)
    {
        var user = RequireUser(p);
        if (user is null) return;
        if (user.Role != "admin")
        {
            await ReplyAsync(p, null, false, "Chi admin");
            return;
        }

        var list = DataStore.LoadUsers()
            .Select(u => UserInfo.From(u, ChatServer.Online.ContainsKey(u.Id)))
            .ToList();
        await ReplyAsync(p, list);
    }

    private async Task HandleGetFile(Packet p)
    {
        var user = RequireUser(p);
        if (user is null) return;

        var data = PacketIO.ParsePayload<GetFilePayload>(p.Payload);
        if (data is null || string.IsNullOrEmpty(data.FilePath))
        {
            await ReplyAsync(p, null, false, "Thiếu đường dẫn file");
            return;
        }

        var full = DataStore.GetUploadFullPath(data.FilePath);
        if (!File.Exists(full))
        {
            await ReplyAsync(p, null, false, "Không tìm thấy file");
            return;
        }

        var bytes = await File.ReadAllBytesAsync(full);
        await ReplyAsync(p, new { base64 = Convert.ToBase64String(bytes), fileName = data.FilePath });
    }

    private async Task HandleAdminLock(Packet p)
    {
        var user = RequireUser(p);
        if (user is null) return;
        if (user.Role != "admin")
        {
            await ReplyAsync(p, null, false, "Chỉ admin");
            return;
        }

        var data = PacketIO.ParsePayload<LockUserPayload>(p.Payload);
        var users = DataStore.LoadUsers();
        var idx = users.FindIndex(u => u.Id == data?.UserId);
        if (idx < 0)
        {
            await ReplyAsync(p, null, false, "Không tìm thấy");
            return;
        }
        if (users[idx].Role == "admin")
        {
            await ReplyAsync(p, null, false, "Không khóa admin");
            return;
        }

        users[idx].Status = users[idx].Status == "locked" ? "active" : "locked";
        DataStore.SaveUsers(users);

        if (users[idx].Status == "locked")
        {
            ChatServer.SendToUser(users[idx].Id, new Packet
            {
                Type = "ACCOUNT_LOCKED",
                Ok = false,
                Error = "Tài khoản đã bị khóa",
            });
        }

        await ReplyAsync(p, UserInfo.From(users[idx]));
    }

    private UserAccount? RequireUser(Packet p)
    {
        var token = GetToken(p);
        if (token == null || !ChatServer.Sessions.TryGetValue(token, out var uid))
        {
            _ = SendAsync(new Packet { Type = p.Type + "_OK", Ok = false, Error = "Chua dang nhap" });
            return null;
        }
        var user = DataStore.LoadUsers().FirstOrDefault(u => u.Id == uid);
        if (user is null || user.Status == "locked")
        {
            _ = SendAsync(new Packet { Type = p.Type + "_OK", Ok = false, Error = "Tài khoản đã bị khóa" });
            return null;
        }
        UserId ??= uid;
        Token ??= token;
        return user;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _stream?.Close();
        _tcp.Close();
    }
}
