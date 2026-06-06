using ChatApp.Client;
using ChatApp.Models;
using ChatApp.Network;
using System.Drawing.Drawing2D;
using System.Text.Json;

namespace ChatApp.UI;

/// <summary>
/// Hỗ trợ hiển thị avatar hình tròn.
/// - CreatePlaceholder: tạo ảnh tạm có chữ cái đầu tên
/// - LoadIntoAsync: tải ảnh từ server qua lệnh GET_FILE
/// </summary>
public static class AvatarHelper
{
    // Cache ảnh đã tải để không tải lại nhiều lần
    private static readonly Dictionary<string, Image> Cache = new();

    /// <summary>Tạo avatar mặc định (nền xanh + chữ cái đầu).</summary>
    public static Image CreatePlaceholder(int size, string letter)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.SteelBlue);
        using var f = new Font("Segoe UI", size / 2.5f, FontStyle.Bold);
        var text = string.IsNullOrEmpty(letter) ? "?" : letter[..1].ToUpperInvariant();
        var sz = g.MeasureString(text, f);
        g.DrawString(text, f, Brushes.White, (size - sz.Width) / 2, (size - sz.Height) / 2);
        return MakeCircular(bmp);
    }

    /// <summary>Cắt ảnh thành hình tròn bằng GraphicsPath.</summary>
    public static Image MakeCircular(Image source)
    {
        var size = Math.Min(source.Width, source.Height);
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = new GraphicsPath();
        path.AddEllipse(0, 0, size - 1, size - 1);
        g.SetClip(path);
        g.DrawImage(source, 0, 0, size, size);
        return bmp;
    }

    /// <summary>Thiết lập PictureBox hiển thị hình tròn.</summary>
    public static void ApplyCircular(PictureBox box, int size)
    {
        box.Size = new Size(size, size);
        box.SizeMode = PictureBoxSizeMode.Zoom;
        UpdateCircularRegion(box);
        box.Resize += (_, _) => UpdateCircularRegion(box);
    }

    private static void UpdateCircularRegion(PictureBox box)
    {
        if (box.Width <= 0 || box.Height <= 0) return;
        using var path = new GraphicsPath();
        path.AddEllipse(0, 0, box.Width - 1, box.Height - 1);
        box.Region = new Region(path);
    }

    /// <summary>Tải avatar từ server và gán vào PictureBox.</summary>
    public static async Task LoadIntoAsync(ChatClient client, PictureBox box, string? avatarPath, string fallbackLetter)
    {
        box.Image?.Dispose();
        var size = box.Width > 0 ? box.Width : 40;
        box.Image = CreatePlaceholder(size, fallbackLetter);

        if (string.IsNullOrEmpty(avatarPath)) return;

        if (Cache.TryGetValue(avatarPath, out var cached))
        {
            box.Image?.Dispose();
            box.Image = MakeCircular((Image)cached.Clone());
            return;
        }

        try
        {
            var resp = await client.SendAndWaitAsync(new Packet
            {
                Type = "GET_FILE",
                Payload = new GetFilePayload { Token = client.Token!, FilePath = avatarPath },
            }, "GET_FILE_OK");

            if (!resp.IsSuccess || resp.Payload is not JsonElement el || !el.TryGetProperty("base64", out var b64))
                return;

            var bytes = Convert.FromBase64String(b64.GetString()!);
            using var ms = new MemoryStream(bytes);
            var img = Image.FromStream(ms);
            Cache[avatarPath] = (Image)img.Clone();
            box.Image?.Dispose();
            box.Image = MakeCircular(img);
        }
        catch
        {
            // Nếu lỗi thì giữ avatar placeholder
        }
    }

    public static void ClearCache(string avatarPath)
    {
        if (Cache.Remove(avatarPath, out var img))
            img.Dispose();
    }
}
