using System.Security.Claims;
using AuthApi.Data;
using AuthApi.Filters;
using AuthApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// Ikki xil chaqiruvchi bor:
// 1) Frontend (mijoz) — "ask"/"thread" orqali, kalitsiz (login shart emas),
//    o'zi savol yozadi va javobni o'qiydi.
// 2) AI chatbot xizmati — "messages" orqali, X-Api-Key bilan himoyalangan,
//    savollarni o'qib javobni ("bot") shu yerga yozadi.
// Ikkalasi ham bitta ChatMessages jadvaliga ulanadi — shu orqali AI xizmati
// bilan frontend bir-birining API kontraktini bilishi shart emas.
[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly AppDbContext _db;
    public ChatController(AppDbContext db) => _db = db;

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
