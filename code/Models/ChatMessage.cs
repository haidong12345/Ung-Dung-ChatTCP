namespace ChatApp.Models;

public class MessageReaction
{
    public string UserId { get; set; } = "";
    public string Emoji { get; set; } = "";
}

public class ChatMessage
{
    public string Id { get; set; } = "";
    public string FromUserId { get; set; } = "";
    public string ToUserId { get; set; } = "";
    public string Type { get; set; } = "text";
    public string Content { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public bool Recalled { get; set; }
    public List<string> SeenBy { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Trích dẫn tin nhắn gốc</summary>
    public string ReplyToId { get; set; } = "";
    public string ReplyPreview { get; set; } = "";

    /// <summary>Emoji phản hồi (👍 ❤️ ...)</summary>
    public List<MessageReaction> Reactions { get; set; } = new();
}
