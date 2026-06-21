namespace ChatApp.Models;

/// <summary>Thông tin user lưu trong file users.json (có mật khẩu đã băm).</summary>
public class UserAccount
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string AvatarPath { get; set; } = "";
    public string Role { get; set; } = "user";   // user | admin
    public string Status { get; set; } = "active"; // active | locked
    public string? ResetCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Thông tin user gửi cho client (KHÔNG gửi mật khẩu).</summary>
public class UserInfo
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string AvatarPath { get; set; } = "";
    public string Role { get; set; } = "";
    public string Status { get; set; } = "";
    public bool Online { get; set; }

    public static UserInfo From(UserAccount u, bool online = false) => new()
    {
        Id = u.Id,
        Username = u.Username,
        DisplayName = u.DisplayName,
        AvatarPath = u.AvatarPath,
        Role = u.Role,
        Status = u.Status,
        Online = online,
    };
}
