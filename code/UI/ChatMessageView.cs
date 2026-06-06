using System.Diagnostics;
using ChatApp.Client;
using ChatApp.Models;
using ChatApp.Network;
using System.Text.Json;

namespace ChatApp.UI;

/// <summary>
/// Vùng hiển thị danh sách tin nhắn (thay cho ListBox).
/// Mỗi tin là 1 Panel: header (giờ, người gửi) + nội dung (text / ảnh / video / file).
/// </summary>
public class ChatMessageView : Panel
{
    private readonly ChatClient _client;
    private readonly Func<string?> _getPartnerName;

    // Lưu panel của từng tin để cập nhật / chọn tin
    private readonly Dictionary<string, Panel> _rowById = new();
    private readonly List<string> _order = new();
    private string? _selectedId;

    public event Action<ChatMessage, MouseEventArgs>? MessageRightClick;

    public ChatMessageView(ChatClient client, Func<string?> getPartnerName)
    {
        _client = client;
        _getPartnerName = getPartnerName;
        AutoScroll = true;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(248, 249, 252);
        Resize += (_, _) => Relayout();
    }

    public void Clear()
    {
        foreach (var row in _rowById.Values)
            DisposeRow(row);
        Controls.Clear();
        _rowById.Clear();
        _order.Clear();
        _selectedId = null;
    }

    /// <summary>Vẽ lại toàn bộ danh sách (dùng khi cập nhật reaction, seen, ...).</summary>
    public void RefreshAll(IReadOnlyDictionary<string, ChatMessage> byId, IReadOnlyList<string> order, string currentUserId)
    {
        Clear();
        foreach (var id in order)
        {
            if (byId.TryGetValue(id, out var m))
                AddMessage(m, currentUserId, scroll: false);
        }
        ScrollToBottom();
    }

    public void AddMessage(ChatMessage m, string currentUserId, bool scroll = true)
    {
        if (_rowById.TryGetValue(m.Id, out var old))
        {
            Controls.Remove(old);
            DisposeRow(old);
            _rowById.Remove(m.Id);
        }
        else if (!_order.Contains(m.Id))
        {
            _order.Add(m.Id);
        }

        var row = BuildRow(m, currentUserId);
        _rowById[m.Id] = row;
        Controls.Add(row);
        Relayout();
        if (scroll) ScrollToBottom();
    }

    public ChatMessage? GetSelectedMessage(IReadOnlyDictionary<string, ChatMessage> byId)
    {
        if (_selectedId == null) return null;
        return byId.GetValueOrDefault(_selectedId);
    }

    private void SelectMessage(string id)
    {
        _selectedId = id;
        foreach (var pair in _rowById)
            pair.Value.BackColor = pair.Key == id ? Color.FromArgb(220, 235, 255) : Color.Transparent;
    }

    /// <summary>Xếp các dòng tin nhắn theo chiều dọc.</summary>
    private void Relayout()
    {
        var width = Math.Max(200, ClientSize.Width - 28);
        var y = 8;
        foreach (var id in _order)
        {
            if (!_rowById.TryGetValue(id, out var row)) continue;
            row.Width = width;
            row.Location = new Point(8, y);
            y += row.Height + 6;
        }
    }

    private void ScrollToBottom()
    {
        if (_order.Count == 0) return;
        if (!_rowById.TryGetValue(_order[^1], out var last)) return;
        ScrollControlIntoView(last);
    }

    /// <summary>Tạo 1 dòng hiển thị cho 1 tin nhắn.</summary>
    private Panel BuildRow(ChatMessage m, string currentUserId)
    {
        var row = new Panel
        {
            Width = Math.Max(200, ClientSize.Width - 28),
            AutoSize = true,
            Padding = new Padding(6),
            Tag = m,
        };

        // Dòng tiêu đề: [HH:mm] Bạn ✓✓ 👍2
        var header = new Label
        {
            AutoSize = false,
            Width = row.Width - 12,
            Height = 22,
            Text = FormatHeader(m, currentUserId),
            Font = new Font(Font.FontFamily, 9f, FontStyle.Bold),
        };
        GanSuKienChonTin(header, m);
        row.Controls.Add(header);

        var y = 24;

        if (m.Recalled)
        {
            var lbl = new Label { Text = "[Tin đã thu hồi]", ForeColor = Color.Gray, Location = new Point(4, y), AutoSize = true };
            GanSuKienChonTin(lbl, m);
            row.Controls.Add(lbl);
            y += 24;
        }
        else
        {
            y = AddQuoteIfAny(row, m, y);
            y = AddContent(row, m, y);
        }

        row.Height = y + 8;
        GanSuKienChonTin(row, m);
        return row;
    }

    private int AddQuoteIfAny(Panel row, ChatMessage m, int y)
    {
        if (string.IsNullOrEmpty(m.ReplyPreview)) return y;

        var quote = new Label
        {
            Text = $"↪ \"{m.ReplyPreview}\"",
            ForeColor = Color.DimGray,
            Location = new Point(4, y),
            AutoSize = true,
            MaximumSize = new Size(row.Width - 16, 0),
        };
        GanSuKienChonTin(quote, m);
        row.Controls.Add(quote);
        return y + quote.Height + 4;
    }

    private int AddContent(Panel row, ChatMessage m, int y)
    {
        if (m.Type == "image" && !string.IsNullOrEmpty(m.FilePath))
            return AddImage(row, m, y);
        if (m.Type == "video" && !string.IsNullOrEmpty(m.FilePath))
            return AddVideoButton(row, m, y);
        if (m.Type == "file")
            return AddFileLink(row, m, y);
        if (!string.IsNullOrEmpty(m.Content))
            return AddText(row, m, y);
        return y;
    }

    private int AddImage(Panel row, ChatMessage m, int y)
    {
        var pic = new PictureBox
        {
            Location = new Point(4, y),
            Size = new Size(220, 160),
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle,
            Cursor = Cursors.Hand,
            BackColor = Color.White,
        };
        pic.Click += async (_, _) => { SelectMessage(m.Id); await PreviewImageAsync(m); };
        pic.MouseUp += (_, e) => { if (e.Button == MouseButtons.Right) ShowRightMenu(m, e); };
        row.Controls.Add(pic);
        _ = LoadThumbnailAsync(pic, m.FilePath);
        return y + pic.Height + 4;
    }

    private int AddVideoButton(Panel row, ChatMessage m, int y)
    {
        var videoBtn = new Button
        {
            Text = $"▶ Video: {m.FileName}",
            Location = new Point(4, y),
            Size = new Size(Math.Min(320, row.Width - 16), 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
        };
        videoBtn.Click += async (_, _) => { SelectMessage(m.Id); await OpenVideoAsync(m); };
        videoBtn.MouseUp += (_, e) => { if (e.Button == MouseButtons.Right) ShowRightMenu(m, e); };
        row.Controls.Add(videoBtn);
        return y + videoBtn.Height + 4;
    }

    private int AddFileLink(Panel row, ChatMessage m, int y)
    {
        var fileLbl = new Label
        {
            Text = $"📎 {m.FileName}",
            Location = new Point(4, y),
            AutoSize = true,
            Cursor = Cursors.Hand,
            ForeColor = Color.RoyalBlue,
        };
        fileLbl.Click += async (_, _) => await SaveFileAsync(m);
        GanSuKienChonTin(fileLbl, m);
        row.Controls.Add(fileLbl);
        return y + 24;
    }

    private int AddText(Panel row, ChatMessage m, int y)
    {
        var body = new Label
        {
            Text = m.Content,
            Location = new Point(4, y),
            AutoSize = true,
            MaximumSize = new Size(row.Width - 16, 0),
        };
        GanSuKienChonTin(body, m);
        row.Controls.Add(body);
        return y + body.Height + 4;
    }

    /// <summary>Gắn sự kiện click chọn tin và chuột phải mở menu.</summary>
    private void GanSuKienChonTin(Control control, ChatMessage m)
    {
        control.Click += (_, _) => SelectMessage(m.Id);
        control.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Right)
                ShowRightMenu(m, e);
        };
    }

    private void ShowRightMenu(ChatMessage m, MouseEventArgs e)
    {
        SelectMessage(m.Id);
        MessageRightClick?.Invoke(m, e);
    }

    private string FormatHeader(ChatMessage m, string currentUserId)
    {
        var who = m.FromUserId == currentUserId ? "Bạn" : _getPartnerName() ?? "?";
        var seen = m.SeenBy.Count > 1 ? " ✓✓" : (m.SeenBy.Contains(currentUserId) ? " ✓" : "");
        var reactions = m.Reactions.Count == 0
            ? ""
            : "  " + string.Join(" ", m.Reactions.GroupBy(r => r.Emoji).Select(g => $"{g.Key}{g.Count()}"));
        return $"[{m.CreatedAt.ToLocalTime():HH:mm}] {who}{seen}{reactions}";
    }

    private async Task LoadThumbnailAsync(PictureBox pic, string filePath)
    {
        try
        {
            var bytes = await DownloadFileBytesAsync(filePath);
            if (bytes is null) return;
            using var ms = new MemoryStream(bytes);
            pic.Image?.Dispose();
            pic.Image = Image.FromStream(ms);
        }
        catch
        {
            // giữ nền trắng nếu tải lỗi
        }
    }

    private async Task PreviewImageAsync(ChatMessage msg)
    {
        var bytes = await DownloadFileBytesAsync(msg.FilePath);
        if (bytes is null) { MessageBox.Show("Không tải được ảnh"); return; }

        using var ms = new MemoryStream(bytes);
        var f = new Form
        {
            Text = msg.FileName,
            Size = new Size(700, 550),
            StartPosition = FormStartPosition.CenterParent,
        };
        f.Controls.Add(new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Image = Image.FromStream(ms) });
        f.ShowDialog();
    }

    private async Task OpenVideoAsync(ChatMessage msg)
    {
        var bytes = await DownloadFileBytesAsync(msg.FilePath);
        if (bytes is null) { MessageBox.Show("Không tải được video"); return; }

        var temp = Path.Combine(Path.GetTempPath(), $"chat_{msg.Id}_{msg.FileName}");
        await File.WriteAllBytesAsync(temp, bytes);
        Process.Start(new ProcessStartInfo(temp) { UseShellExecute = true });
    }

    private async Task SaveFileAsync(ChatMessage msg)
    {
        var bytes = await DownloadFileBytesAsync(msg.FilePath);
        if (bytes is null) { MessageBox.Show("Không tải được file"); return; }

        using var dlg = new SaveFileDialog { FileName = msg.FileName };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        await File.WriteAllBytesAsync(dlg.FileName, bytes);
    }

    /// <summary>Gửi GET_FILE tới server, nhận file dạng base64.</summary>
    private async Task<byte[]?> DownloadFileBytesAsync(string filePath)
    {
        var resp = await _client.SendAndWaitAsync(new Packet
        {
            Type = "GET_FILE",
            Payload = new GetFilePayload { Token = _client.Token!, FilePath = filePath },
        }, "GET_FILE_OK");

        if (!resp.IsSuccess || resp.Payload is not JsonElement el || !el.TryGetProperty("base64", out var b64El))
            return null;
        return Convert.FromBase64String(b64El.GetString()!);
    }

    private static void DisposeRow(Panel row)
    {
        foreach (Control c in row.Controls)
        {
            if (c is PictureBox pb)
                pb.Image?.Dispose();
        }
        row.Dispose();
    }
}
