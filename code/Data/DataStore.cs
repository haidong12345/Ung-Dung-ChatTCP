using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChatApp.Models;

namespace ChatApp.Data;

/// <summary>
/// Lưu trữ dữ liệu bằng file JSON (đơn giản, dễ xem khi demo).
/// - data/users.json     : danh sách tài khoản
/// - data/messages.json  : lịch sử tin nhắn
/// - uploads/            : ảnh, video, file đã upload
/// </summary>
public static class DataStore
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private static readonly string UsersFile = Path.Combine(DataDir, "users.json");
    private static readonly string MessagesFile = Path.Combine(DataDir, "messages.json");
    public static readonly string UploadDir = Path.Combine(AppContext.BaseDirectory, "uploads");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    static DataStore()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(UploadDir);
        EnsureAdmin(); // tạo tài khoản admin mặc định nếu chưa có
    }

    /// <summary>Băm mật khẩu bằng SHA-256 (demo; thực tế nên dùng bcrypt/argon2).</summary>
    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Tạo id ngắn 16 ký tự cho user/tin nhắn.</summary>
    public static string NewId() => Guid.NewGuid().ToString("N")[..16];

    private static void EnsureAdmin()
    {
        var users = LoadUsers();
        if (users.Any(u => u.Role == "admin")) return;

        users.Add(new UserAccount
        {
            Id = NewId(),
            Username = "admin",
            PasswordHash = HashPassword("admin123"),
            DisplayName = "Quan tri vien",
            Role = "admin",
            Status = "active",
        });
        SaveUsers(users);
    }

    public static List<UserAccount> LoadUsers()
    {
        if (!File.Exists(UsersFile)) return new();
        return JsonSerializer.Deserialize<List<UserAccount>>(File.ReadAllText(UsersFile), JsonOpts) ?? new();
    }

    public static void SaveUsers(List<UserAccount> users) =>
        File.WriteAllText(UsersFile, JsonSerializer.Serialize(users, JsonOpts));

    public static List<ChatMessage> LoadMessages()
    {
        if (!File.Exists(MessagesFile)) return new();
        return JsonSerializer.Deserialize<List<ChatMessage>>(File.ReadAllText(MessagesFile), JsonOpts) ?? new();
    }

    public static void SaveMessages(List<ChatMessage> messages) =>
        File.WriteAllText(MessagesFile, JsonSerializer.Serialize(messages, JsonOpts));

    /// <summary>Lưu file upload, trả về tên file mới (tránh trùng tên).</summary>
    public static string SaveUpload(string fileName, byte[] data)
    {
        var ext = Path.GetExtension(fileName);
        var saved = $"{DateTime.UtcNow.Ticks}_{NewId()}{ext}";
        var path = Path.Combine(UploadDir, saved);
        File.WriteAllBytes(path, data);
        return saved;
    }

    public static string GetUploadFullPath(string fileName) =>
        Path.Combine(UploadDir, fileName);
}
