using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Serialization;
using AuthApi.Data;
using AuthApi.Filters;
using AuthApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// Ikki xil chaqiruvchi bor:
// 1) Frontend (mijoz) — "ask"/"thread" orqali, kalitsiz (login shart emas),
//    o'zi savol yozadi va javobni o'qiydi. "ask" endi ICHKI tarmoqdagi AI
//    xizmatiga (FastAPI, "AI-chi" tomonidan yozilgan, 192.168.89.6:8003 —
//    o'z-o'zicha Supabase'dan o'qib+OpenAI orqali javob tayyorlaydi) server-
//    to-server so'rov yuborib, javobni darhol "bot" xabari sifatida shu
//    yerga (ChatMessages) saqlaydi — frontend buni odatdagidek "thread"
//    orqali (pastda) polling bilan ko'radi.
// 2) "messages" — X-Api-Key bilan himoyalangan, boshqa/eski integratsiyalar
//    uchun qoldirilgan (masalan AI xizmati o'zi to'g'ridan-to'g'ri yozmoqchi
//    bo'lsa ham ishlaydi), lekin hozir asosiy oqim "ask" orqali.
[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChatController> _logger;

    public ChatController(AppDbContext db, IHttpClientFactory httpClientFactory, ILogger<ChatController> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private record AiAskRequest([property: JsonPropertyName("conversationId")] string ConversationId, [property: JsonPropertyName("message")] string Message);
    private record AiAskResponse([property: JsonPropertyName("conversationId")] string? ConversationId, [property: JsonPropertyName("answer")] string? Answer);

    // Mijoz savol yozadi. Sender/UserId mijozdan qabul qilinmaydi — server
    // o'zi belgilaydi (spoofing'ning oldini olish uchun).
    [HttpPost("ask")]
    public async Task<IActionResult> Ask(AskChatDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ConversationId))
        {
            return BadRequest(new { message = "conversationId kiritilishi shart." });
        }

        if (string.IsNullOrWhiteSpace(dto.Text))
        {
            return BadRequest(new { message = "text kiritilishi shart." });
        }

        var message = new ChatMessage
        {
            ConversationId = dto.ConversationId,
            UserId = GetUserId(),
            Sender = "user",
            Text = dto.Text
        };

        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync();

        // AI javobini olib bo'lmasa ham (xizmat vaqtincha ishlamasa), foydalanuvchi
        // xabari baribir saqlanib qoladi — bu yerdagi xato "ask"ning o'zini
        // 500'ga chiqarmasligi kerak, shuning uchun alohida try/catch.
        try
        {
            var client = _httpClientFactory.CreateClient("ChatbotAi");
            var response = await client.PostAsJsonAsync("api/chatbot/ask", new AiAskRequest(dto.ConversationId, dto.Text));

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AiAskResponse>();
                if (!string.IsNullOrWhiteSpace(result?.Answer))
                {
                    _db.ChatMessages.Add(new ChatMessage
                    {
                        ConversationId = dto.ConversationId,
                        Sender = "bot",
                        Text = result.Answer
                    });
                    await _db.SaveChangesAsync();
                }
            }
            else
            {
                _logger.LogWarning("Chatbot AI xizmati kutilmagan status qaytardi: {Status}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chatbot AI xizmatiga (ichki tarmoq) ulanib bo'lmadi.");
        }

        return Ok(message);
    }

    // Widget shu orqali suhbatni o'qib/yangilab turadi (bot javobi kelganda
    // ham shu yerdan ko'rinadi).
    [HttpGet("thread")]
    public async Task<IActionResult> GetThread([FromQuery] string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return BadRequest(new { message = "conversationId kiritilishi shart." });
        }

        var messages = await _db.ChatMessages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        return Ok(messages);
    }

    private int? GetUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out var id) ? id : null;
    }

    [RequireChatApiKey]
    [HttpPost("messages")]
    public async Task<IActionResult> CreateMessage(CreateChatMessageDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ConversationId))
        {
            return BadRequest(new { message = "conversationId kiritilishi shart." });
        }

        if (dto.Sender != "user" && dto.Sender != "bot")
        {
            return BadRequest(new { message = "sender \"user\" yoki \"bot\" bo'lishi kerak." });
        }

        if (string.IsNullOrWhiteSpace(dto.Text))
        {
            return BadRequest(new { message = "text kiritilishi shart." });
        }

        var message = new ChatMessage
        {
            ConversationId = dto.ConversationId,
            UserId = dto.UserId,
            Sender = dto.Sender,
            Text = dto.Text
        };

        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync();

        return Ok(message);
    }

    [RequireChatApiKey]
    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages([FromQuery] string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return BadRequest(new { message = "conversationId kiritilishi shart." });
        }

        var messages = await _db.ChatMessages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        return Ok(messages);
    }
}
