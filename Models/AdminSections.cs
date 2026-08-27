namespace AuthApi.Models;

// Frontend'dagi admin/src/lib/session.ts bilan AYNAN mos bo'lishi kerak —
// bo'lim kalitlari, taqiqlangan va beriladigan ro'yxatlar shu yerdan kelib chiqadi.
public static class AdminSections
{
    public static readonly string[] All =
    {
        "analytics", "products", "reviews", "orders", "customers", "gifts", "campaigns", "banners", "users", "coupons", "translations"
    };

    // Superuserga berilishi mumkin bo'lgan bo'limlar — analytics bundan mustasno
    // (u barcha kirgan foydalanuvchilar uchun doim ochiq, alohida saqlanmaydi).
    // Taqiqlangan bo'limlar yo'q — qaysi bo'limlarni berish admin'ning o'zi tanlaydi.
    public static readonly string[] SuperuserGrantable = All.Where(s => s != "analytics").ToArray();

    // Yangi superuser yaratilganda taklif etiladigan boshlang'ich (xavfsiz) tanlov.
    public static readonly string[] SuperuserDefaultGrants = { "products", "reviews", "gifts", "campaigns", "banners" };

    public static List<string> Sanitize(string role, IEnumerable<string>? requested)
    {
        if (role != "Superuser" || requested == null) return new List<string>();
        return requested
            .Where(p => SuperuserGrantable.Contains(p))
            .Distinct()
            .ToList();
    }
}
