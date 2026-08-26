namespace AuthApi.Models;

public class CreateChatMessageDto
{
    public string ConversationId { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
