using System.Diagnostics;
using ChatApp.Client;
using ChatApp.Models;
using ChatApp.Network;
using System.Text.Json;

namespace ChatApp.UI;

/// <summary>Panel cuộn hiển thị tin nhắn kèm ảnh/video inline</summary>
public class ChatMessageView : Panel
{
    private readonly ChatClient _client;
    private readonly Func<string?> _getPartnerName;
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
        foreach (var (mid, row) in _rowById)
            row.BackColor = mid == id ? Color.FromArgb(220, 235, 255) : Color.Transparent;
    }

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

    private Panel BuildRow(ChatMessage m, string currentUserId)
    {
        var row = new Panel
        {
            Width = Math.Max(200, ClientSize.Width - 28),
            AutoSize = true,
            Padding = new Padding(6),
            Tag = m,
        };

        void Wire(Control c)
        {
            c.MouseUp += (_, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    SelectMessage(m.Id);
                    MessageRightClick?.Invoke(m, e);
                }
            };
            c.Click += (_, _) => SelectMessage(m.Id);
        }

        var header = new Label
        {
            AutoSize = false,
            Width = row.Width - 12,
            Height = 22,
            Text = FormatHeader(m, currentUserId),
            Font = new Font(Font.FontFamily, 9f, FontStyle.Bold),
        };
        Wire(header);
        row.Controls.Add(header);

        var y = 24;

        if (m.Recalled)
        {
            var lbl = new Label { Text = "[Tin đã thu hồi]", ForeColor = Color.Gray, Location = new Point(4, y), AutoSize = true };
            Wire(lbl);
            row.Controls.Add(lbl);
            y += 24;
        }
        else
        {
            if (!string.IsNullOrEmpty(m.ReplyPreview))
            {
                var quote = new Label
                {
                    Text = $"↪ \"{m.ReplyPreview}\"",
                    ForeColor = Color.DimGray,
                    Location = new Point(4, y),
                    AutoSize = true,
                    MaximumSize = new Size(row.Width - 16, 0),
                };
                Wire(quote);
                row.Controls.Add(quote);
                y += quote.Height + 4;
            }

            if (m.Type == "image" && !string.IsNullOrEmpty(m.FilePath))
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
                pic.Click += async (_, _) =>
                {
                    SelectMessage(m.Id);
                    await PreviewImageAsync(m);
                };
                pic.MouseUp += (_, e) =>
                {
                    if (e.Button == MouseButtons.Right)
                    {
                        SelectMessage(m.Id);
                        MessageRightClick?.Invoke(m, e);
                    }
                };
                row.Controls.Add(pic);
                _ = LoadThumbnailAsync(pic, m.FilePath);
                y += pic.Height + 4;
            }
            else if (m.Type == "video" && !string.IsNullOrEmpty(m.FilePath))
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
                videoBtn.Click += async (_, _) =>
                {
                    SelectMessage(m.Id);
                    await OpenVideoAsync(m);
                };
                videoBtn.MouseUp += (_, e) =>
                {
                    if (e.Button == MouseButtons.Right)
                    {
                        SelectMessage(m.Id);
                        MessageRightClick?.Invoke(m, e);
                    }
                };
                row.Controls.Add(videoBtn);
                y += videoBtn.Height + 4;
            }
            else if (m.Type == "file")
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
                Wire(fileLbl);
                row.Controls.Add(fileLbl);
                y += 24;
            }
            else if (!string.IsNullOrEmpty(m.Content))
            {
                var body = new Label
                {
                    Text = m.Content,
                    Location = new Point(4, y),
                    AutoSize = true,
                    MaximumSize = new Size(row.Width - 16, 0),
                };
                Wire(body);
                row.Controls.Add(body);
                y += body.Height + 4;
            }
        }

        row.Height = y + 8;
        Wire(row);
        return row;
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
            // giữ nền trắng
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
