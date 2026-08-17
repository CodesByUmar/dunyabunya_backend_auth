using System.Security.Claims;
using AuthApi.Data;
using AuthApi.Filters;
using AuthApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// Mahsulot sharhlari. O'qish — ochiq. Yozish — kirgan foydalanuvchi.
// Javob yozish/o'chirish — Admin yoki "reviews" ruxsati bor Superuser.
[ApiController]
[Route("api/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ReviewsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetReviews([FromQuery] int? productId)
    {
        var query = _db.Reviews.AsQueryable();
        if (productId.HasValue) query = query.Where(r => r.ProductId == productId.Value);

        var reviews = await query.OrderByDescending(r => r.Date).ToListAsync();
        return Ok(reviews);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateReview(CreateReviewDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var user = await _db.Users.FindAsync(userId.Value);
        if (user == null) return Unauthorized();

        var product = await _db.Products.FindAsync(dto.ProductId);
        if (product == null) return NotFound(new { message = "Mahsulot topilmadi." });

        if (dto.Rating < 1 || dto.Rating > 5)
        {
            return BadRequest(new { message = "Baho 1 dan 5 gacha bo'lishi kerak." });
        }

        var review = new Review
        {
            ProductId = dto.ProductId,
            ProductName = product.Name,
            Author = $"{user.FirstName} {user.LastName}".Trim(),
            UserId = user.Id,
            City = dto.City,
            Rating = dto.Rating,
            Text = dto.Text,
            Pros = dto.Pros,
            Cons = dto.Cons,
            Photos = dto.Photos ?? Array.Empty<string>(),
            Recommends = dto.Recommends
        };

        _db.Reviews.Add(review);
        await _db.SaveChangesAsync();

        await RecalculateProductRatingAsync(dto.ProductId);

        return Ok(review);
    }

    [RequireSection("reviews")]
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> UpdateReview(int id, UpdateReviewDto dto)
    {
        var review = await _db.Reviews.FindAsync(id);
        if (review == null) return NotFound(new { message = "Sharh topilmadi." });

        var isNewReply = dto.Reply != null && review.Reply == null;

        if (dto.Reply != null) review.Reply = dto.Reply;
        if (dto.Likes.HasValue) review.Likes = dto.Likes.Value;
        if (dto.Dislikes.HasValue) review.Dislikes = dto.Dislikes.Value;

        await _db.SaveChangesAsync();

        // Birinchi marta javob yozilganda, sharh egasiga bildirishnoma yuboriladi.
        if (isNewReply && review.UserId.HasValue)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = review.UserId.Value,
                Type = "review_reply",
                ProductId = review.ProductId,
                ReviewId = review.Id,
                ProductName = review.ProductName,
                ReplyText = dto.Reply!
            });
            await _db.SaveChangesAsync();
        }

        return Ok(review);
    }

    [RequireSection("reviews")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteReview(int id)
    {
        var review = await _db.Reviews.FindAsync(id);
        if (review == null) return NotFound(new { message = "Sharh topilmadi." });

        var productId = review.ProductId;
        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync();

        await RecalculateProductRatingAsync(productId);

        return Ok(new { message = "O'chirildi." });
    }

    private async Task RecalculateProductRatingAsync(int productId)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product == null) return;

        var stats = await _db.Reviews
            .Where(r => r.ProductId == productId)
            .GroupBy(r => 1)
            .Select(g => new { Count = g.Count(), Avg = g.Average(r => r.Rating) })
            .FirstOrDefaultAsync();

        product.ReviewCount = stats?.Count ?? 0;
        product.Rating = stats != null ? Math.Round(stats.Avg, 1) : 0;

        await _db.SaveChangesAsync();
    }

    private int? GetUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out var id) ? id : null;
    }
}
