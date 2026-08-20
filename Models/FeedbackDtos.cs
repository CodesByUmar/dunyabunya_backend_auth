namespace AuthApi.Models;

public class CreateReviewDto
{
    public int ProductId { get; set; }
    public string? City { get; set; }
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Pros { get; set; }
    public string? Cons { get; set; }
    public string[]? Photos { get; set; }
    public bool Recommends { get; set; } = true;
}

// Admin/Superuser sharhga javob yozganda yoki like/dislike yangilaganda ishlatiladi.
public class UpdateReviewDto
{
    public string? Reply { get; set; }
    public int? Likes { get; set; }
    public int? Dislikes { get; set; }
}

// Oddiy mijozning like/dislike bosishi uchun — UpdateReviewDto'dan farqli
// o'laroq, son to'g'ridan-to'g'ri berilmaydi, faqat "like" | "dislike" turi.
public class VoteReviewDto
{
    public string Type { get; set; } = string.Empty;
}

public class CreateServiceReviewDto
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

public class CreateContactMessageDto
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
