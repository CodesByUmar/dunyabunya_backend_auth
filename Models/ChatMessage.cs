namespace AuthApi.Models;

// AI chatbot suhbat tarixi. Suhbatning o'zi (LLM chaqiruvi, javob generatsiyasi)
// butunlay tashqi AI xizmatida amalga oshadi — bu yerda faqat tarix (log) sifatida
// saqlanadi, AI xizmati har bir xabarni (mijozniki ham, botniki ham) shu yerga yozadi.
public class ChatMessage
{
    public int Id { get; set; }

    // AI xizmati tomonidan generatsiya qilingan, bitta suhbatni birlashtiruvchi ID
    // (mehmon foydalanuvchi uchun ham ishlaydi — login talab qilinmaydi).
    public string ConversationId { get; set; } = string.Empty;

    // Agar suhbat paytida foydalanuvchi login qilingan bo'lsa — bog'lash uchun (ixtiyoriy).
    public int? UserId { get; set; }

    public string Sender { get; set; } = string.Empty; // "user" | "bot"
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
