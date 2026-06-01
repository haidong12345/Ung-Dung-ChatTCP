using ChatApp.Client;
using ChatApp.Models;
using ChatApp.Network;

namespace ChatApp.UI;

/// <summary>Màn hình chat chính: danh sách user, tin nhắn, realtime</summary>
public class MainForm : Form
{
    private readonly ChatClient _client;
    private readonly ListBox _lstUsers = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly ListBox _lstMessages = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly TextBox _txtInput = new() { Dock = DockStyle.Fill };
    private readonly Label _lblTyping = new() { Text = "", Height = 22, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label _lblChatHeader = new() { Height = 28, TextAlign = ContentAlignment.MiddleLeft, Text = "  Chọn người bên trái để chat" };
    private readonly Label _lblStatus = new() { Height = 24, TextAlign = ContentAlignment.MiddleLeft };

    private List<UserInfo> _users = new();
    private UserInfo? _chatWith;
    private readonly Dictionary<string, ChatMessage> _messagesById = new();
    private readonly List<string> _messageOrder = new();
    private System.Windows.Forms.Timer? _typingTimer;

    public MainForm(ChatClient client, List<UserInfo>? initialUsers = null)
    {
        _client = client;
        Text = $"Chat - {client.CurrentUser?.DisplayName}";
        Size = new Size(900, 600);
        MinimumSize = new Size(640, 400);
        StartPosition = FormStartPosition.CenterScreen;

        BuildMenu();

        _lblStatus.Text = $"  Xin chào {client.CurrentUser?.DisplayName}";
        _lblStatus.Dock = DockStyle.Top;

        var main = new Panel { Dock = DockStyle.Fill };

        var left = new Panel { Dock = DockStyle.Left, Width = 150 };
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
        _lblChatHeader.Dock = DockStyle.Top;
        _lblTyping.Dock = DockStyle.Bottom;

        var inputPanel = new Panel { Dock = DockStyle.Bottom, Height = 100 };
        var btnRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(2) };
        btnRow.Controls.Add(MakeBtn("😀", InsertEmoji));
        btnRow.Controls.Add(MakeBtn("Ảnh", SendImage));
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

        _lstMessages.Dock = DockStyle.Fill;
        _lstMessages.Font = new Font(Font.FontFamily, 10f);
        right.Controls.Add(_lstMessages);
        right.Controls.Add(inputPanel);
        right.Controls.Add(_lblTyping);
        right.Controls.Add(_lblChatHeader);

        main.Controls.Add(right);
        main.Controls.Add(left);

        Controls.Add(main);
        Controls.Add(_lblStatus);

        _lstMessages.DoubleClick += (_, _) => PreviewSelected();
        _client.OnPacket += OnServerPacket;

        ApplyUserList(initialUsers ?? new());
        Shown += (_, _) => _ = RefreshUsersAsync();
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

    private async Task LoadChatAsync()
    {
        int idx = _lstUsers.SelectedIndex;
        if (idx < 0 || _lstUsers.Tag is not List<UserInfo> list || idx >= list.Count || _users.Count == 0) return;

        _chatWith = list[idx];
        _lblChatHeader.Text = $"  Đang chat với: {_chatWith.DisplayName}";

        _lstMessages.Items.Clear();
        _messagesById.Clear();
        _messageOrder.Clear();

        var resp = await _client.SendAndWaitAsync(new Packet
        {
            Type = "GET_HISTORY",
            Payload = new HistoryPayload { Token = _client.Token!, OtherUserId = _chatWith.Id },
        }, "GET_HISTORY_OK");

        if (!resp.IsSuccess) return;
        var history = PacketIO.ParsePayload<List<ChatMessage>>(resp.Payload) ?? new();
        foreach (var m in history)
            AddMessage(m, isNew: false);

        await MarkSeenAsync();
    }

    private void AddMessage(ChatMessage m, bool isNew)
    {
        _messagesById[m.Id] = m;
        if (_chatWith != null &&
            !((m.FromUserId == _client.CurrentUser!.Id && m.ToUserId == _chatWith.Id) ||
              (m.FromUserId == _chatWith.Id && m.ToUserId == _client.CurrentUser!.Id)))
            return;

        _lstMessages.Items.Add(FormatMessage(m));
        if (!_messageOrder.Contains(m.Id))
            _messageOrder.Add(m.Id);

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

    private string FormatMessage(ChatMessage m)
    {
        if (m.Recalled) return "[Tin đã thu hồi]";
        var who = m.FromUserId == _client.CurrentUser?.Id ? "Bạn" : _chatWith?.DisplayName ?? "?";
        var seen = m.SeenBy.Count > 1 ? " ✓✓" : (m.SeenBy.Contains(_client.CurrentUser?.Id ?? "") ? " ✓" : "");
        return m.Type switch
        {
            "image" => $"[{m.CreatedAt:HH:mm}] {who}{seen}: [Ảnh] {m.FileName} (double-click xem)",
            "file" => $"[{m.CreatedAt:HH:mm}] {who}{seen}: [File] {m.FileName}",
            _ => $"[{m.CreatedAt:HH:mm}] {who}{seen}: {m.Content}",
        };
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

        await SendMessageAsync("text", _txtInput.Text.Trim(), "", "");
        _txtInput.Clear();
        await SendTypingAsync(false);
    }

    private async Task SendMessageAsync(string messageType, string content, string filePath, string fileName)
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
        var emojis = new[] { "😀", "😂", "❤️", "👍", "🎉", "😢", "🔥", "✨" };
        var menu = new ContextMenuStrip();
        foreach (var em in emojis)
            menu.Items.Add(em, null, (_, _) => _txtInput.AppendText(em));
        menu.Show(_txtInput, 0, 0);
    }

    private async void SendImage(object? sender, EventArgs e)
    {
        if (_chatWith is null) return;
        using var dlg = new OpenFileDialog { Filter = "Ảnh|*.jpg;*.jpeg;*.png;*.gif" };
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
        int idx = _lstMessages.SelectedIndex;
        if (idx < 0 || _chatWith is null || idx >= _messageOrder.Count) return;

        var msg = _messagesById.GetValueOrDefault(_messageOrder[idx]);
        if (msg is null || msg.FromUserId != _client.CurrentUser?.Id) return;

        await _client.SendAndWaitAsync(new Packet
        {
            Type = "RECALL_MESSAGE",
            Payload = new RecallPayload { Token = _client.Token!, MessageId = msg.Id, ToUserId = _chatWith.Id },
        }, "RECALL_MESSAGE_OK");
        MarkRecalled(msg.Id);
    }

    private async void PreviewSelected()
    {
        int idx = _lstMessages.SelectedIndex;
        if (idx < 0 || idx >= _messageOrder.Count) return;
        var msg = _messagesById.GetValueOrDefault(_messageOrder[idx]);
        if (msg?.Type != "image" || string.IsNullOrEmpty(msg.FilePath)) return;

        var resp = await _client.SendAndWaitAsync(new Packet
        {
            Type = "GET_FILE",
            Payload = new GetFilePayload { Token = _client.Token!, FilePath = msg.FilePath },
        }, "GET_FILE_OK");

        if (!resp.IsSuccess) { MessageBox.Show(resp.Error); return; }
        if (resp.Payload is not System.Text.Json.JsonElement el || !el.TryGetProperty("base64", out var b64El))
            return;

        var bytes = Convert.FromBase64String(b64El.GetString()!);
        using var ms = new MemoryStream(bytes);
        var f = new Form { Text = "Xem ảnh", Size = new Size(600, 500), StartPosition = FormStartPosition.CenterParent };
        f.Controls.Add(new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Image = Image.FromStream(ms) });
        f.ShowDialog();
    }

    private void EditProfile()
    {
        var f = new Form { Text = "Hồ sơ", Size = new Size(360, 200), FormBorderStyle = FormBorderStyle.FixedDialog };
        var name = new TextBox { Text = _client.CurrentUser?.DisplayName ?? "", Width = 220, Location = new Point(100, 20) };
        var btnAvatar = new Button { Text = "Chọn avatar", Location = new Point(20, 60) };
        string? avatarB64 = null, avatarName = null;
        btnAvatar.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = "Ảnh|*.png;*.jpg" };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                avatarB64 = Convert.ToBase64String(File.ReadAllBytes(dlg.FileName));
                avatarName = Path.GetFileName(dlg.FileName);
            }
        };
        var btnSave = new Button { Text = "Lưu", Location = new Point(20, 100) };
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
                if (u != null) _client.CurrentUser!.DisplayName = u.DisplayName;
                MessageBox.Show("Đã cập nhật");
                f.Close();
            }
        };
        f.Controls.AddRange(new Control[] { new Label { Text = "Tên hiển thị:", Location = new Point(20, 24), AutoSize = true }, name, btnAvatar, btnSave });
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
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _client.OnPacket -= OnServerPacket;
        base.OnFormClosed(e);
    }
}
