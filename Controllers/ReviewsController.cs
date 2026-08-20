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

    // Oddiy mijozning like/dislike bosishi. Bir foydalanuvchi bir sharhga
    // faqat bitta ovoz bera oladi (ReviewVote'dagi UNIQUE (ReviewId, UserId)
    // orqali bazada ham majburlangan): birinchi bosish — ovoz qo'shadi, xuddi
    // shu turdagi ovozni qayta bosish — bekor qiladi (toggle), boshqa turini
    // bosish — ovozni almashtiradi. Sonlar atomik SQL orqali o'zgaradi, shuning
    // uchun bir vaqtda kelgan bir nechta so'rov ham noto'g'ri sanoqqa olib kelmaydi.
    [Authorize]
    [HttpPost("{id:int}/vote")]
    public async Task<IActionResult> VoteReview(int id, VoteReviewDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (dto.Type != "like" && dto.Type != "dislike")
        {
            return BadRequest(new { message = "Ovoz turi \"like\" yoki \"dislike\" bo'lishi kerak." });
        }

        var reviewExists = await _db.Reviews.AnyAsync(r => r.Id == id);
        if (!reviewExists) return NotFound(new { message = "Sharh topilmadi." });

        var existingVote = await _db.ReviewVotes.FirstOrDefaultAsync(v => v.ReviewId == id && v.UserId == userId.Value);

        if (existingVote == null)
        {
            // Birinchi ovoz — qo'shamiz va hisoblagichni oshiramiz.
            try
            {
                _db.ReviewVotes.Add(new ReviewVote { ReviewId = id, UserId = userId.Value, Type = dto.Type });
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Bir vaqtda kelgan ikkinchi so'rov UNIQUE cheklovga uchradi — demak
                // ovoz allaqachon yozilgan, xatoni yutib, oddiy holatni qaytaramiz.
                return await GetVoteStateAsync(id, userId.Value);
            }

            await IncrementAsync(id, dto.Type, +1);
        }
        else if (existingVote.Type == dto.Type)
        {
            // Xuddi shu ovozni qayta bosish — bekor qilish (toggle off).
            _db.ReviewVotes.Remove(existingVote);
            await _db.SaveChangesAsync();
            await IncrementAsync(id, dto.Type, -1);
        }
        else
        {
            // Boshqa turdagi ovozga almashtirish.
            var oldType = existingVote.Type;
            existingVote.Type = dto.Type;
            await _db.SaveChangesAsync();
            await IncrementAsync(id, oldType, -1);
            await IncrementAsync(id, dto.Type, +1);
        }

        return await GetVoteStateAsync(id, userId.Value);
    }

    private async Task IncrementAsync(int reviewId, string type, int delta)
    {
        if (type == "like")
        {
            await _db.Reviews.Where(r => r.Id == reviewId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Likes, r => r.Likes + delta));
        }
        else
        {
            await _db.Reviews.Where(r => r.Id == reviewId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Dislikes, r => r.Dislikes + delta));
        }
    }

    private async Task<IActionResult> GetVoteStateAsync(int reviewId, int userId)
    {
        var review = await _db.Reviews.AsNoTracking().FirstAsync(r => r.Id == reviewId);
        var myVote = await _db.ReviewVotes.AsNoTracking()
            .Where(v => v.ReviewId == reviewId && v.UserId == userId)
            .Select(v => v.Type)
            .FirstOrDefaultAsync();

        return Ok(new { likes = review.Likes, dislikes = review.Dislikes, myVote });
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
