using System.Text.Json;
using ChatApp.Client;
using ChatApp.Models;
using ChatApp.Network;

namespace ChatApp.UI;

/// <summary>
/// Màn hình chat chính.
/// Bố cục: trái = danh sách user | phải = tin nhắn + ô nhập
/// Luồng chính:
/// 1. Chọn user -> LoadChatAsync (GET_HISTORY)
/// 2. Gõ tin -> SendTextAsync (SEND_MESSAGE)
/// 3. Server đẩy NEW_MESSAGE -> OnServerPacket -> AddMessage
/// </summary>
public class MainForm : Form
{
    public bool LoggedOut { get; private set; }

    private readonly ChatClient _client;
    private readonly ListBox _lstUsers = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly ChatMessageView _msgView;
    private readonly TextBox _txtInput = new() { Dock = DockStyle.Fill };
    private readonly Label _lblTyping = new() { Text = "", Height = 22, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label _lblChatHeader = new() { Height = 28, TextAlign = ContentAlignment.MiddleLeft, Text = "  Chọn người bên trái để chat" };
    private readonly Label _lblStatus = new() { Height = 24, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label _lblMyName = new() { AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
    private readonly PictureBox _picMyAvatar = new() { Location = new Point(10, 8) };
    private readonly PictureBox _picPartnerAvatar = new() { Location = new Point(4, 2) };
    private readonly Panel _pnlQuote = new() { Dock = DockStyle.Top, Height = 44, Visible = false, BackColor = Color.FromArgb(240, 248, 255) };
    private readonly Label _lblQuote = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };

    private ChatMessage? _quoteMsg;
    private static readonly string[] ReactionEmojis = { "👍", "❤️", "😂", "😮", "😢", "🔥", "👏", "🎉" };
    private static readonly string[] InputEmojis = { "😀", "😂", "😍", "👍", "🙏", "❤️", "🔥", "🎉", "😢", "😡", "✨", "💯" };

    // Dữ liệu chat đang mở
    private List<UserInfo> _users = new();
    private UserInfo? _chatWith; // người đang chat cùng
    private readonly Dictionary<string, ChatMessage> _messagesById = new(); // tra cứu tin theo id
    private readonly List<string> _messageOrder = new(); // thứ tự hiển thị
    private System.Windows.Forms.Timer? _typingTimer;

    public MainForm(ChatClient client, List<UserInfo>? initialUsers = null)
    {
        _client = client;
        _msgView = new ChatMessageView(_client, () => _chatWith?.DisplayName);
        Text = $"Chat - {client.CurrentUser?.DisplayName}";
        Size = new Size(900, 600);
        MinimumSize = new Size(640, 400);
        StartPosition = FormStartPosition.CenterScreen;

        BuildMenu();

        AvatarHelper.ApplyCircular(_picMyAvatar, 40);
        AvatarHelper.ApplyCircular(_picPartnerAvatar, 36);
        _lblMyName.Text = client.CurrentUser?.DisplayName ?? "?";
        _lblMyName.Location = new Point(58, 18);
var pnlMyProfile = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.FromArgb(240, 244, 250) };
        pnlMyProfile.Controls.Add(_picMyAvatar);
        pnlMyProfile.Controls.Add(_lblMyName);

        _lblStatus.Text = "  Chọn người bên trái để xem lịch sử trò chuyện";
        _lblStatus.Dock = DockStyle.Top;

        var main = new Panel { Dock = DockStyle.Fill };

        var left = new Panel { Dock = DockStyle.Left, Width = 260, MinimumSize = new Size(260, 0) };
        var leftTop = new Panel { Dock = DockStyle.Top, Height = 30 };
        leftTop.Controls.Add(new Label { Text = "Người chat", Location = new Point(4, 8), AutoSize = true });
        var btnRefresh = new Button { Text = "↻", Size = new Size(32, 24), Location = new Point(108, 3) };
        btnRefresh.Click += (_, _) => _ = RefreshUsersAsync();
        leftTop.Controls.Add(btnRefresh);
        _lstUsers.Dock = DockStyle.Fill;
        _lstUsers.Font = new Font(Font.FontFamily, 9f);
        left.Controls.Add(_lstUsers);
        left.Controls.Add(leftTop);
        _lstUsers.SelectedIndexChanged += (_, _) => _ = LoadChatAsync();

        var right = new Panel { Dock = DockStyle.Fill };
        var headerPanel = new Panel { Dock = DockStyle.Top, Height = 0, Visible = false };
        _lblChatHeader.AutoSize = false;
        _lblChatHeader.Dock = DockStyle.None;
        _lblChatHeader.Location = new Point(44, 10);
        _lblChatHeader.Width = 400;
        //headerPanel.Controls.Add(_picPartnerAvatar);
        //headerPanel.Controls.Add(_lblChatHeader);
        _lblTyping.Dock = DockStyle.Bottom;

        var btnCancelQuote = new Button { Text = "✕", Dock = DockStyle.Right, Width = 36, FlatStyle = FlatStyle.Flat };
        btnCancelQuote.Click += (_, _) => ClearQuote();
        _pnlQuote.Controls.Add(btnCancelQuote);
        _pnlQuote.Controls.Add(_lblQuote);

        var inputPanel = new Panel { Dock = DockStyle.Bottom, Height = 130 };
        var btnRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(2) };
        btnRow.Controls.Add(MakeBtn("😀", InsertEmoji));
        btnRow.Controls.Add(MakeBtn("Trích dẫn", QuoteSelected));
        btnRow.Controls.Add(MakeBtn("Ảnh", SendImage));
        btnRow.Controls.Add(MakeBtn("Video", SendVideo));
        btnRow.Controls.Add(MakeBtn("File", SendFile));
        btnRow.Controls.Add(MakeBtn("Thu hồi", RecallSelected));
        btnRow.Controls.Add(MakeBtn("Gửi", (_, _) => _ = SendTextAsync()));

        _txtInput.Multiline = true;
        _txtInput.Dock = DockStyle.Fill;
        _txtInput.ScrollBars = ScrollBars.Vertical;
        _txtInput.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                await SendTextAsync();
            }
            else
                _ = SendTypingAsync(true);
        };
        inputPanel.Controls.Add(_txtInput);
inputPanel.Controls.Add(btnRow);
        inputPanel.Controls.Add(_pnlQuote);

        _msgView.MessageRightClick += OnMessageRightClick;
        right.Controls.Add(_msgView);
        right.Controls.Add(inputPanel);
        right.Controls.Add(_lblTyping);
        right.Controls.Add(headerPanel);

        main.Controls.Add(right);
        main.Controls.Add(left);

        Controls.Add(main);
        Controls.Add(_lblStatus);
        Controls.Add(pnlMyProfile);

        _client.OnPacket += OnServerPacket;

        ApplyUserList(initialUsers ?? new());
        Shown += async (_, _) =>
        {
            await AvatarHelper.LoadIntoAsync(_client, _picMyAvatar, _client.CurrentUser?.AvatarPath, _client.CurrentUser?.DisplayName ?? "?");
            _ = RefreshUsersAsync();
        };
    }

    private void ApplyUserList(List<UserInfo> users)
    {
        _users = users;
        _lstUsers.Items.Clear();
        foreach (var u in _users)
            _lstUsers.Items.Add($"{(u.Online ? "🟢" : "⚫")} {u.DisplayName} ({u.Username})");
        _lstUsers.Tag = _users;

        if (_users.Count == 0)
        {
            _lstUsers.Items.Add("— Chưa có ai khác —");
            _lblStatus.Text = "  Mở thêm 1 client, đăng ký user khác, bấm ↻";
        }
        else if (_lstUsers.SelectedIndex < 0)
            _lstUsers.SelectedIndex = 0;
    }

    private Button MakeBtn(string text, EventHandler click)
    {
        var b = new Button { Text = text, AutoSize = true, Margin = new Padding(3) };
        b.Click += click;
        return b;
    }

    private void BuildMenu()
    {
        var menu = new MenuStrip();
        var mFile = new ToolStripMenuItem("Tài khoản");
        mFile.DropDownItems.Add("Hồ sơ / Avatar", null, (_, _) => EditProfile());
        mFile.DropDownItems.Add("Tải lại lịch sử chat", null, async (_, _) => await LoadChatAsync());
        mFile.DropDownItems.Add("Đổi mật khẩu", null, (_, _) => ChangePassword());
        mFile.DropDownItems.Add("Đăng xuất", null, async (_, _) => await LogoutAsync());
        menu.Items.Add(mFile);

        if (_client.CurrentUser?.Role == "admin")
        {
            menu.Items.Add(new ToolStripMenuItem("Admin", null, (_, _) =>
            {
                new AdminForm(_client).ShowDialog();
                _ = RefreshUsersAsync();
            }));
        }
        MainMenuStrip = menu;
        Controls.Add(menu);
    }

    /// <summary>
    /// Server gửi gói tin bất kỳ lúc nào (tin mới, typing, ...).
    /// OnPacket chạy trên luồng nền -> phải Invoke về luồng UI.
    /// </summary>
    private void OnServerPacket(Packet p)
    {
        if (InvokeRequired) { BeginInvoke(() => OnServerPacket(p)); return; }

        switch ((p.Type ?? "").ToUpperInvariant())
        {
            case "NEW_MESSAGE":
                var msg = PacketIO.ParsePayload<ChatMessage>(p.Payload);
                if (msg != null) AddMessage(msg, isNew: true);
                break;
case "MESSAGE_RECALLED":
                if (p.Payload is System.Text.Json.JsonElement el && el.TryGetProperty("messageId", out var mid))
                    MarkRecalled(mid.GetString()!);
                break;
            case "TYPING":
                if (p.Payload is System.Text.Json.JsonElement te)
                {
                    var from = te.GetProperty("fromUserId").GetString();
                    var typing = te.GetProperty("isTyping").GetBoolean();
                    if (_chatWith?.Id == from)
                        _lblTyping.Text = typing ? $"{_chatWith?.DisplayName} đang nhập..." : "";
                }
                break;
            case "MESSAGES_SEEN":
                RefreshSeenMarks();
                break;
            case "USER_STATUS":
                _ = RefreshUsersAsync();
                break;
            case "ACCOUNT_LOCKED":
                MessageBox.Show("Tài khoản đã bị khóa!");
                Close();
                break;
            case "MESSAGE_REACTION":
                if (p.Payload is JsonElement re &&
                    re.TryGetProperty("messageId", out var rid) &&
                    re.TryGetProperty("reactions", out var rlist))
                {
                    var reactions = JsonSerializer.Deserialize<List<MessageReaction>>(rlist.GetRawText()) ?? new();
                    ApplyReaction(rid.GetString()!, reactions);
                }
                break;
            case "PROFILE_UPDATED":
                var pu = PacketIO.ParsePayload<UserInfo>(p.Payload);
                if (pu is null) break;
                if (pu.Id == _client.CurrentUser?.Id)
                {
                    _client.CurrentUser!.DisplayName = pu.DisplayName;
                    _client.CurrentUser.AvatarPath = pu.AvatarPath;
                    _lblMyName.Text = pu.DisplayName;
                    Text = $"Chat - {pu.DisplayName}";
                    AvatarHelper.ClearCache(pu.AvatarPath);
                    _ = AvatarHelper.LoadIntoAsync(_client, _picMyAvatar, pu.AvatarPath, pu.DisplayName);
                }
                if (_chatWith?.Id == pu.Id)
                {
                    _chatWith.AvatarPath = pu.AvatarPath;
                    _ = AvatarHelper.LoadIntoAsync(_client, _picPartnerAvatar, pu.AvatarPath, pu.DisplayName);
                }
                _ = RefreshUsersAsync();
                break;
        }
    }

    private async Task RefreshUsersAsync()
    {
        var resp = await _client.SendAndWaitAsync(new Packet
        {
            Type = "GET_USERS",
            Payload = new TokenPayload { Token = _client.Token! },
        }, "GET_USERS_OK");

        if (!resp.IsSuccess)
        {
            _lblStatus.Text = "  Lỗi tải danh sách: " + (resp.Error ?? "?");
            return;
        }
        ApplyUserList(PacketIO.ParseUserList(resp.Payload));
    }
/// <summary>Tải lịch sử chat với user đang chọn (lệnh GET_HISTORY).</summary>
    private async Task LoadChatAsync()
    {
        int idx = _lstUsers.SelectedIndex;
        if (idx < 0 || _lstUsers.Tag is not List<UserInfo> list || idx >= list.Count || _users.Count == 0) return;

        _chatWith = list[idx];
        //_lblChatHeader.Text = $"  Đang chat với: {_chatWith.DisplayName}";
        ClearQuote();
        await AvatarHelper.LoadIntoAsync(_client, _picPartnerAvatar, _chatWith.AvatarPath, _chatWith.DisplayName);

        _msgView.Clear();
        _messagesById.Clear();
        _messageOrder.Clear();

        var resp = await _client.SendAndWaitAsync(new Packet
        {
            Type = "GET_HISTORY",
            Payload = new HistoryPayload { Token = _client.Token!, OtherUserId = _chatWith.Id },
        }, "GET_HISTORY_OK");

        if (!resp.IsSuccess)
        {
            _lblStatus.Text = "  Không tải được lịch sử: " + (resp.Error ?? "?");
            return;
        }
        var history = PacketIO.ParsePayload<List<ChatMessage>>(resp.Payload) ?? new();
        foreach (var m in history)
        {
            _messagesById[m.Id] = m;
            _messageOrder.Add(m.Id);
        }
        _msgView.RefreshAll(_messagesById, _messageOrder, _client.CurrentUser!.Id);

        _lblStatus.Text = history.Count == 0
            ? $"  Chưa có tin nhắn với {_chatWith.DisplayName}"
            : $"  Lịch sử: {history.Count} tin với {_chatWith.DisplayName}";

        await MarkSeenAsync();
    }

    /// <summary>Thêm 1 tin vào bộ nhớ và vẽ lên màn hình (nếu thuộc cuộc chat hiện tại).</summary>
    private void AddMessage(ChatMessage m, bool isNew)
    {
        _messagesById[m.Id] = m;
        if (!IsMessageInCurrentChat(m)) return;

        if (!_messageOrder.Contains(m.Id))
            _messageOrder.Add(m.Id);
        _msgView.AddMessage(m, _client.CurrentUser!.Id);

        if (isNew && m.FromUserId != _client.CurrentUser?.Id)
        {
            if (!Focused)
            {
                var preview = m.Type == "text" ? m.Content : $"[{m.Type}]";
                MessageBox.Show(preview, $"Tin mới từ {_chatWith?.DisplayName}");
            }
            _ = MarkSeenAsync();
        }
    }

    /// <summary>Kiểm tra tin có thuộc cuộc chat 1-1 đang mở không.</summary>
    private bool IsMessageInCurrentChat(ChatMessage m)
    {
        if (_chatWith is null) return false;
        var me = _client.CurrentUser!.Id;
        return (m.FromUserId == me && m.ToUserId == _chatWith.Id)
            || (m.FromUserId == _chatWith.Id && m.ToUserId == me);
    }

    private void RefreshMessageListUI()
    {
        _msgView.RefreshAll(_messagesById, _messageOrder, _client.CurrentUser!.Id);
    }

    private void ApplyReaction(string messageId, List<MessageReaction> reactions)
    {
        if (!_messagesById.TryGetValue(messageId, out var m)) return;
        m.Reactions = reactions;
RefreshMessageListUI();
    }

    private ChatMessage? GetSelectedMessage() =>
        _msgView.GetSelectedMessage(_messagesById);

    private void SetQuote(ChatMessage msg)
    {
        _quoteMsg = msg;
        _pnlQuote.Visible = true;
        _lblQuote.Text = "  Trích dẫn: " + (msg.Recalled ? "[đã thu hồi]" : msg.Type == "text" ? msg.Content : $"[{msg.Type}] {msg.FileName}");
    }

    private void ClearQuote()
    {
        _quoteMsg = null;
        _pnlQuote.Visible = false;
        _lblQuote.Text = "";
    }

    private void QuoteSelected(object? sender, EventArgs e)
    {
        var msg = GetSelectedMessage();
        if (msg is null || msg.Recalled) { MessageBox.Show("Chọn tin nhắn để trích dẫn"); return; }
        SetQuote(msg);
        _txtInput.Focus();
    }

    private void OnMessageRightClick(ChatMessage msg, MouseEventArgs e)
    {
        var menu = new ContextMenuStrip();
        if (!msg.Recalled)
        {
            menu.Items.Add("↪ Trích dẫn", null, (_, _) => SetQuote(msg));
            var reactMenu = new ToolStripMenuItem("Emoji phản hồi");
            foreach (var em in ReactionEmojis)
            {
                var emoji = em;
                reactMenu.DropDownItems.Add(emoji, null, async (_, _) => await SendEmojiReplyAsync(msg, emoji));
            }
            menu.Items.Add(reactMenu);
        }
        menu.Items.Add("Thu hồi", null, async (_, _) => await RecallMessageAsync(msg));
        menu.Show(_msgView, _msgView.PointToClient(Cursor.Position));
    }

    private async Task SendEmojiReplyAsync(ChatMessage msg, string emoji)
    {
        if (_chatWith is null) return;
        var resp = await _client.SendAndWaitAsync(new Packet
        {
            Type = "EMOJI_REPLY",
            Payload = new EmojiReplyPayload
            {
                Token = _client.Token!,
                MessageId = msg.Id,
                ToUserId = _chatWith.Id,
                Emoji = emoji,
            },
        }, "EMOJI_REPLY_OK");

        if (!resp.IsSuccess) { MessageBox.Show(resp.Error); return; }
        if (resp.Payload is System.Text.Json.JsonElement el &&
            el.TryGetProperty("messageId", out var mid) &&
            el.TryGetProperty("reactions", out var rlist))
        {
            var reactions = JsonSerializer.Deserialize<List<MessageReaction>>(rlist.GetRawText()) ?? new();
            ApplyReaction(mid.GetString()!, reactions);
        }
    }

    private async Task RecallMessageAsync(ChatMessage msg)
    {
        if (_chatWith is null || msg.FromUserId != _client.CurrentUser?.Id) return;
        await _client.SendAndWaitAsync(new Packet
        {
            Type = "RECALL_MESSAGE",
            Payload = new RecallPayload { Token = _client.Token!, MessageId = msg.Id, ToUserId = _chatWith.Id },
        }, "RECALL_MESSAGE_OK");
        MarkRecalled(msg.Id);
    }

    private void MarkRecalled(string messageId)
    {
if (_messagesById.TryGetValue(messageId, out var m))
        {
            m.Recalled = true;
            _ = LoadChatAsync();
        }
    }

    private void RefreshSeenMarks() => _ = LoadChatAsync();

    private async Task SendTextAsync()
    {
        if (_chatWith is null || _users.Count == 0)
        {
            MessageBox.Show("Chọn một người bên trái trước khi gửi tin.", "Chat");
            return;
        }
        if (string.IsNullOrWhiteSpace(_txtInput.Text)) return;

        await SendMessageAsync("text", _txtInput.Text.Trim(), "", "", _quoteMsg?.Id ?? "");
        _txtInput.Clear();
        ClearQuote();
        await SendTypingAsync(false);
    }

    private async Task SendMessageAsync(string messageType, string content, string filePath, string fileName, string replyToId = "")
    {
        var resp = await _client.SendAndWaitAsync(new Packet
        {
            Type = "SEND_MESSAGE",
            Payload = new SendMessagePayload
            {
                Token = _client.Token!,
                ToUserId = _chatWith!.Id,
                MessageType = messageType,
                Content = content,
                FilePath = filePath,
                FileName = fileName,
                ReplyToId = replyToId,
            },
        }, "SEND_MESSAGE_OK");

        if (!resp.IsSuccess)
        {
            MessageBox.Show(resp.Error ?? "Gửi tin thất bại", "Lỗi");
            return;
        }

        var msg = PacketIO.ParsePayload<ChatMessage>(resp.Payload);
        if (msg != null) AddMessage(msg, isNew: false);
    }

    private async Task SendTypingAsync(bool isTyping)
    {
        if (_chatWith is null) return;
        _typingTimer?.Stop();
        _typingTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _typingTimer.Tick += async (_, _) =>
        {
            _typingTimer.Stop();
            await _client.SendAsync(new Packet
            {
                Type = "TYPING",
                Payload = new TypingPayload { Token = _client.Token!, ToUserId = _chatWith!.Id, IsTyping = false },
            });
        };
        if (isTyping)
        {
            await _client.SendAsync(new Packet
            {
                Type = "TYPING",
                Payload = new TypingPayload { Token = _client.Token!, ToUserId = _chatWith.Id, IsTyping = true },
            });
            _typingTimer.Start();
        }
    }

    private async Task MarkSeenAsync()
    {
        if (_chatWith is null) return;
        var ids = _messagesById.Values
            .Where(m => m.FromUserId == _chatWith.Id && !m.SeenBy.Contains(_client.CurrentUser!.Id))
            .Select(m => m.Id).ToList();
        if (ids.Count == 0) return;

        await _client.SendAsync(new Packet
        {
            Type = "SEEN",
            Payload = new SeenPayload { Token = _client.Token!, ToUserId = _chatWith.Id, MessageIds = ids },
        });
    }

    private void InsertEmoji(object? sender, EventArgs e)
{
        var menu = new ContextMenuStrip();
        foreach (var em in InputEmojis)
            menu.Items.Add(em, null, (_, _) => _txtInput.AppendText(em));
        menu.Show(_txtInput, 0, 0);
    }

    private async void SendImage(object? sender, EventArgs e)
    {
        if (_chatWith is null) return;
        using var dlg = new OpenFileDialog { Filter = "Ảnh|*.jpg;*.jpeg;*.png;*.gif;*.webp" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        await UploadAndSendAsync(dlg.FileName);
    }

    private async void SendVideo(object? sender, EventArgs e)
    {
        if (_chatWith is null) return;
        using var dlg = new OpenFileDialog { Filter = "Video|*.mp4;*.webm;*.avi;*.mov;*.mkv" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        await UploadAndSendAsync(dlg.FileName);
    }

    private async void SendFile(object? sender, EventArgs e)
    {
        if (_chatWith is null) return;
        using var dlg = new OpenFileDialog();
        if (dlg.ShowDialog() != DialogResult.OK) return;
        await UploadAndSendAsync(dlg.FileName);
    }

    /// <summary>Bước 1: UPLOAD file lên server. Bước 2: SEND_MESSAGE với đường dẫn file.</summary>
    private async Task UploadAndSendAsync(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path);
        var b64 = Convert.ToBase64String(bytes);
        var name = Path.GetFileName(path);

        var resp = await _client.SendAndWaitAsync(new Packet
        {
            Type = "UPLOAD",
            Payload = new UploadPayload { Token = _client.Token!, FileName = name, Base64Data = b64 },
        }, "UPLOAD_OK");

        if (!resp.IsSuccess) { MessageBox.Show(resp.Error); return; }
        var up = PacketIO.ParsePayload<UploadResult>(resp.Payload);
        if (up is null) return;
        await SendMessageAsync(up.Type, "", up.FilePath, name);
    }

    private async void RecallSelected(object? sender, EventArgs e)
    {
        if (_chatWith is null) return;
        var msg = GetSelectedMessage();
        if (msg is null || msg.FromUserId != _client.CurrentUser?.Id) return;

        await _client.SendAndWaitAsync(new Packet
        {
            Type = "RECALL_MESSAGE",
            Payload = new RecallPayload { Token = _client.Token!, MessageId = msg.Id, ToUserId = _chatWith.Id },
        }, "RECALL_MESSAGE_OK");
        MarkRecalled(msg.Id);
    }

    private void EditProfile()
    {
        var f = new Form { Text = "Hồ sơ", Size = new Size(400, 240), FormBorderStyle = FormBorderStyle.FixedDialog };
        var name = new TextBox { Text = _client.CurrentUser?.DisplayName ?? "", Width = 220, Location = new Point(100, 20) };
        var picPreview = new PictureBox { Location = new Point(280, 20), Size = new Size(72, 72) };
        AvatarHelper.ApplyCircular(picPreview, 72);
        _ = AvatarHelper.LoadIntoAsync(_client, picPreview, _client.CurrentUser?.AvatarPath, _client.CurrentUser?.DisplayName ?? "?");
var btnAvatar = new Button { Text = "Chọn avatar", Location = new Point(20, 60), Width = 120 };
        string? avatarB64 = null, avatarName = null;
        btnAvatar.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = "Ảnh|*.png;*.jpg;*.jpeg;*.webp" };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                avatarB64 = Convert.ToBase64String(File.ReadAllBytes(dlg.FileName));
                avatarName = Path.GetFileName(dlg.FileName);
                using var img = Image.FromFile(dlg.FileName);
                picPreview.Image?.Dispose();
                picPreview.Image = AvatarHelper.MakeCircular(img);
            }
        };
        var btnSave = new Button { Text = "Lưu", Location = new Point(20, 120) };
        btnSave.Click += async (_, _) =>
        {
            var resp = await _client.SendAndWaitAsync(new Packet
            {
                Type = "UPDATE_PROFILE",
                Payload = new ProfilePayload { Token = _client.Token!, DisplayName = name.Text, AvatarBase64 = avatarB64, AvatarFileName = avatarName },
            }, "UPDATE_PROFILE_OK");
            if (resp.IsSuccess)
            {
                var u = PacketIO.ParsePayload<UserInfo>(resp.Payload);
                if (u != null)
                {
                    _client.CurrentUser!.DisplayName = u.DisplayName;
                    _client.CurrentUser.AvatarPath = u.AvatarPath;
                    _lblMyName.Text = u.DisplayName;
                    Text = $"Chat - {u.DisplayName}";
                    if (!string.IsNullOrEmpty(u.AvatarPath))
                        AvatarHelper.ClearCache(u.AvatarPath);
                    await AvatarHelper.LoadIntoAsync(_client, _picMyAvatar, u.AvatarPath, u.DisplayName);
                }
                MessageBox.Show("Đã cập nhật");
                f.Close();
            }
        };
        f.Controls.AddRange(new Control[]
        {
            new Label { Text = "Tên hiển thị:", Location = new Point(20, 24), AutoSize = true },
            name, btnAvatar, btnSave, picPreview,
            new Label { Text = "Xem trước", Location = new Point(280, 96), AutoSize = true },
        });
        f.ShowDialog();
    }

    private void ChangePassword()
    {
        var f = new Form { Text = "Đổi mật khẩu", Size = new Size(340, 180), FormBorderStyle = FormBorderStyle.FixedDialog };
        var oldP = new TextBox { UseSystemPasswordChar = true, Width = 200, Location = new Point(100, 20) };
        var newP = new TextBox { UseSystemPasswordChar = true, Width = 200, Location = new Point(100, 55) };
        var btn = new Button { Text = "Lưu", Location = new Point(100, 95) };
        btn.Click += async (_, _) =>
        {
            var resp = await _client.SendAndWaitAsync(new Packet
            {
                Type = "CHANGE_PASSWORD",
Payload = new ChangePasswordPayload { Token = _client.Token!, OldPassword = oldP.Text, NewPassword = newP.Text },
            }, "CHANGE_PASSWORD_OK");
            MessageBox.Show(resp.IsSuccess ? "Đã đổi mật khẩu thành công!" : resp.Error);
            if (resp.IsSuccess) f.Close();
        };
        f.Controls.AddRange(new Control[] { new Label { Text = "MK cũ:", Location = new Point(20, 24), AutoSize = true }, oldP, new Label { Text = "MK mới:", Location = new Point(20, 59), AutoSize = true }, newP, btn });
        f.ShowDialog();
    }

    private async Task LogoutAsync()
    {
        await _client.SendAndWaitAsync(new Packet
        {
            Type = "LOGOUT",
            Payload = new TokenPayload { Token = _client.Token! },
        }, "LOGOUT_OK");
        _client.Dispose();
        LoggedOut = true;
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _client.OnPacket -= OnServerPacket;
        base.OnFormClosed(e);
    }
}


