namespace ChatApp.Models;

/// <summary>Emoji phản hồi trên 1 tin nhắn (👍, ❤️, ...).</summary>
public class MessageReaction
{
    public string UserId { get; set; } = "";
    public string Emoji { get; set; } = "";
}

/// <summary>1 tin nhắn trong cuộc trò chuyện 1-1.</summary>
public class ChatMessage
{
    public string Id { get; set; } = "";
    public string FromUserId { get; set; } = "";
    public string ToUserId { get; set; } = "";

    /// <summary>Loại tin: text, image, video, file</summary>
    public string Type { get; set; } = "text";
    public string Content { get; set; } = "";

    /// <summary>Đường dẫn file trên server (trong thư mục uploads/)</summary>
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";

    public bool Recalled { get; set; }

    /// <summary>Danh sách userId đã xem tin này</summary>
    public List<string> SeenBy { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Trích dẫn: id tin gốc và nội dung rút gọn hiển thị</summary>
    public string ReplyToId { get; set; } = "";
    public string ReplyPreview { get; set; } = "";

    public List<MessageReaction> Reactions { get; set; } = new();
}
