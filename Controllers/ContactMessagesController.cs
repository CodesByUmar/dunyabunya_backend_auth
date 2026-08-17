using AuthApi.Data;
using AuthApi.Filters;
using AuthApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// "Aloqa" sahifasidan yuborilgan xabarlar. Yuborish — ochiq (mehmon ham
// yozishi mumkin). Ko'rish — Admin yoki "reviews" ruxsati bor Superuser
// (admin panelda "Sharxlar" bo'limining "Savollar" ichki qismida ko'rinadi).
[ApiController]
[Route("api/[controller]")]
public class ContactMessagesController : ControllerBase
{
    private readonly AppDbContext _db;
    public ContactMessagesController(AppDbContext db) => _db = db;

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateContactMessageDto dto)
    {
        var message = new ContactMessage
        {
            Name = dto.Name,
            Phone = dto.Phone,
            Message = dto.Message
        };

        _db.ContactMessages.Add(message);
        await _db.SaveChangesAsync();

        return Ok(message);
    }

    [RequireSection("reviews")]
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.ContactMessages.OrderByDescending(m => m.Date).ToListAsync());
}
