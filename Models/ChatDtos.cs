namespace AuthApi.Models;

public class CreateChatMessageDto
{
    public string ConversationId { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

// Frontend (mijoz) o'z savolini yozganda ishlatadi — Sender/UserId'ni o'zi
// belgilay olmaydi (aks holda "bot" nomidan yozib qo'yishi mumkin edi),
// ular server tomonida avtomatik belgilanadi.
public class AskChatDto
{
    public string ConversationId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
