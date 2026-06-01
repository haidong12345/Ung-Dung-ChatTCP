namespace ChatApp.Models;

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
}
