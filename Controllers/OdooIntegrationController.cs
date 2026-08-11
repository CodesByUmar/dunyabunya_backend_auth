using AuthApi.Data;
using AuthApi.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// Odoo tomonidan chaqiriladigan endpointlar — marketplace foydalanuvchilarini
// tekshirish/kuzatish uchun. Barchasi RequireOdooApiKey bilan himoyalangan.
[ApiController]
[Route("api/odoo")]
[RequireOdooApiKey]
public class OdooIntegrationController : ControllerBase
{
    private readonly AppDbContext _db;

    public OdooIntegrationController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { ok = true, message = "API key to'g'ri qabul qilindi." });

    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts([FromQuery] int page = 1, [FromQuery] int limit = 100)
    {
        page = Math.Max(page, 1);
        limit = Math.Clamp(limit, 1, 500);

        var query = _db.Users.OrderBy(u => u.Id);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(u => new
            {
                user_id = u.Id,
                odoo_partner_id = u.OdooPartnerId,
                full_name = (u.FirstName + " " + u.LastName).Trim(),
                phone = u.PhoneNumber,
                email = u.Email,
                role = u.Role,
                created_at = u.CreatedAt
            })
            .ToListAsync();

        return Ok(new { items, page, total });
    }

    [HttpGet("contacts/unsynced")]
    public async Task<IActionResult> GetUnsyncedContacts([FromQuery] int page = 1, [FromQuery] int limit = 100)
    {
        page = Math.Max(page, 1);
        limit = Math.Clamp(limit, 1, 500);

        var query = _db.Users.Where(u => u.OdooPartnerId == null).OrderBy(u => u.Id);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(u => new
            {
                user_id = u.Id,
                full_name = (u.FirstName + " " + u.LastName).Trim(),
                phone = u.PhoneNumber,
                email = u.Email,
                role = u.Role,
                created_at = u.CreatedAt
            })
            .ToListAsync();

        return Ok(new { items, page, total });
    }

    [HttpGet("contacts/{userId:int}")]
    public async Task<IActionResult> GetContact(int userId)
    {
        var user = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                user_id = u.Id,
                odoo_partner_id = u.OdooPartnerId,
                full_name = (u.FirstName + " " + u.LastName).Trim(),
                phone = u.PhoneNumber,
                email = u.Email,
                role = u.Role,
                created_at = u.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (user == null) return NotFound(new { message = "Foydalanuvchi topilmadi." });

        return Ok(user);
    }
}
