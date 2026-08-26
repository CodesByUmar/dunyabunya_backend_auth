using AuthApi.Data;
using AuthApi.Filters;
using AuthApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// AI chatbot xizmati tomonidan chaqiriladi — suhbatning o'zi (LLM javobi) tashqi
// xizmatda amalga oshadi, bu yerda faqat tarix sifatida saqlanadi/o'qiladi.
// Barcha endpointlar RequireChatApiKey bilan himoyalangan (Odoo integratsiyasi
// bilan bir xil naqsh) — oddiy foydalanuvchi JWT tokeni bilan emas.
[ApiController]
[Route("api/chat")]
[RequireChatApiKey]
public class ChatController : ControllerBase
{
    private readonly AppDbContext _db;
    public ChatController(AppDbContext db) => _db = db;

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
