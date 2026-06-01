using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChatApp.Models;

namespace ChatApp.Data;

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
        EnsureAdmin();
    }

    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

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
            DisplayName = "Quản trị viên",
            Role = "admin",
            Status = "active",
        });
        SaveUsers(users);
    }

    public static List<UserAccount> LoadUsers() =>
        File.Exists(UsersFile)
            ? JsonSerializer.Deserialize<List<UserAccount>>(File.ReadAllText(UsersFile), JsonOpts) ?? new()
            : new();

    public static void SaveUsers(List<UserAccount> users) =>
        File.WriteAllText(UsersFile, JsonSerializer.Serialize(users, JsonOpts));

    public static List<ChatMessage> LoadMessages() =>
        File.Exists(MessagesFile)
            ? JsonSerializer.Deserialize<List<ChatMessage>>(File.ReadAllText(MessagesFile), JsonOpts) ?? new()
            : new();

    public static void SaveMessages(List<ChatMessage> messages) =>
        File.WriteAllText(MessagesFile, JsonSerializer.Serialize(messages, JsonOpts));

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
