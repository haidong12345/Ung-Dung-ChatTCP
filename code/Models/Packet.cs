namespace ChatApp.Models;

public class Packet
{
    public string Type { get; set; } = "";
    public object? Payload { get; set; }
    public bool Ok { get; set; } = true;
    public string? Error { get; set; }

    public bool IsSuccess =>
        string.IsNullOrEmpty(Error) &&
        (Ok || (Type ?? "").EndsWith("_OK", StringComparison.OrdinalIgnoreCase));
}

public class AuthPayload
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? DisplayName { get; set; }
}

public class TokenPayload
{
    public string Token { get; set; } = "";
}

public class HistoryPayload
{
    public string Token { get; set; } = "";
    public string OtherUserId { get; set; } = "";
}

public class GetFilePayload
{
    public string Token { get; set; } = "";
    public string FilePath { get; set; } = "";
}

public class ChangePasswordPayload
{
    public string Token { get; set; } = "";
    public string OldPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

public class AuthResult
{
    public string Token { get; set; } = "";
    public UserInfo User { get; set; } = new();
    public List<UserInfo> Users { get; set; } = new();
}

public class SendMessagePayload
{
    public string Token { get; set; } = "";
    public string ToUserId { get; set; } = "";
    public string MessageType { get; set; } = "text";
    public string Content { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ReplyToId { get; set; } = "";
}

public class EmojiReplyPayload
{
    public string Token { get; set; } = "";
    public string MessageId { get; set; } = "";
    public string ToUserId { get; set; } = "";
    public string Emoji { get; set; } = "";
}

public class RecallPayload
{
    public string Token { get; set; } = "";
    public string MessageId { get; set; } = "";
    public string ToUserId { get; set; } = "";
}

public class TypingPayload
{
    public string Token { get; set; } = "";
    public string ToUserId { get; set; } = "";
    public bool IsTyping { get; set; }
}

public class SeenPayload
{
    public string Token { get; set; } = "";
    public string ToUserId { get; set; } = "";
    public List<string> MessageIds { get; set; } = new();
}

public class UploadPayload
{
    public string Token { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Base64Data { get; set; } = "";
}

public class UploadResult
{
    public string FilePath { get; set; } = "";
    public string Type { get; set; } = "file";
}

public class ForgotPayload
{
    public string Username { get; set; } = "";
    public string? ResetCode { get; set; }
    public string? NewPassword { get; set; }
}

public class ProfilePayload
{
    public string Token { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? AvatarBase64 { get; set; }
    public string? AvatarFileName { get; set; }
}

public class LockUserPayload
{
    public string Token { get; set; } = "";
    public string UserId { get; set; } = "";
}
