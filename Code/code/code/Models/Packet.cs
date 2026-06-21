namespace ChatApp.Models;

/// <summary>Gói tin gửi qua TCP. Mọi lệnh đều có dạng này.</summary>
public class Packet
{
    /// <summary>Tên lệnh: LOGIN, SEND_MESSAGE, GET_HISTORY, ...</summary>
    public string Type { get; set; } = "";

    /// <summary>Dữ liệu kèm theo (username, token, nội dung tin, ...).</summary>
    public object? Payload { get; set; }

    public bool Ok { get; set; } = true;
    public string? Error { get; set; }

    /// <summary>Server trả lỗi khi Ok=false hoặc Type kết thúc bằng _OK.</summary>
    public bool IsSuccess =>
        string.IsNullOrEmpty(Error) &&
        (Ok || (Type ?? "").EndsWith("_OK", StringComparison.OrdinalIgnoreCase));
}

#region Payload đăng nhập / tài khoản

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

public class AuthResult
{
    public string Token { get; set; } = "";
    public UserInfo User { get; set; } = new();
    public List<UserInfo> Users { get; set; } = new();
}

public class ChangePasswordPayload
{
    public string Token { get; set; } = "";
    public string OldPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
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

#endregion

#region Payload chat

public class HistoryPayload
{
    public string Token { get; set; } = "";
    public string OtherUserId { get; set; } = "";
}

public class SendMessagePayload
{
    public string Token { get; set; } = "";
    public string ToUserId { get; set; } = "";
    public string MessageType { get; set; } = "text"; // text | image | video | file
    public string Content { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ReplyToId { get; set; } = "";
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

public class EmojiReplyPayload
{
    public string Token { get; set; } = "";
    public string MessageId { get; set; } = "";
    public string ToUserId { get; set; } = "";
    public string Emoji { get; set; } = "";
}

#endregion

#region Payload file

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

public class GetFilePayload
{
    public string Token { get; set; } = "";
    public string FilePath { get; set; } = "";
}

#endregion

#region Payload admin

public class LockUserPayload
{
    public string Token { get; set; } = "";
    public string UserId { get; set; } = "";
}

#endregion
