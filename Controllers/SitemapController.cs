using System.Security;
using System.Text;
using AuthApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// SEO uchun — Google/Yandex'ga saytdagi barcha ochiq sahifalarni (bosh sahifa,
// kategoriyalar, har bir tasdiqlangan mahsulot) ro'yxat qilib beradi. Ochiq
// (autentifikatsiya shart emas) — bu sof ommaviy ma'lumot, /api/products bilan bir xil.
//
// MUHIM: frontend domenida (demo.dunyabunya.uz) nginx faqat "/auth-api/*"ni shu
// backend'ga yo'naltiradi, shuning uchun bu sahifa haqiqiy manzilda
// "https://demo.dunyabunya.uz/sitemap.xml" emas, balki
// "https://demo.dunyabunya.uz/auth-api/api/sitemap.xml" da turadi. Bu — sitemap
// protokoliga zid emas (robots.txt istalgan manzilga ishora qila oladi), faqat
// frontend'ning robots.txt fayli shu TO'LIQ manzilni ko'rsatishi kerak.
[ApiController]
public class SitemapController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public SitemapController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpGet("/api/sitemap.xml")]
    public async Task<IActionResult> GetSitemap()
    {
        var baseUrl = (_config["Frontend:BaseUrl"] ?? "https://demo.dunyabunya.uz").TrimEnd('/');

        var products = await _db.Products
            .Where(p => p.IsOnline && p.IsPublishedInOdoo)
            .Select(p => new { p.Id, p.UpdatedAt })
            .ToListAsync();

        var categories = await _db.Categories
            .Include(c => c.Subcategories)
            .OrderBy(c => c.Order)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        sb.Append("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");

        void AddUrl(string path, DateTime? lastmod, string changefreq, string priority)
        {
            var loc = SecurityElement.Escape(baseUrl + path);
            sb.Append("  <url>\n");
            sb.Append($"    <loc>{loc}</loc>\n");
            if (lastmod.HasValue) sb.Append($"    <lastmod>{lastmod.Value:yyyy-MM-dd}</lastmod>\n");
            sb.Append($"    <changefreq>{changefreq}</changefreq>\n");
            sb.Append($"    <priority>{priority}</priority>\n");
            sb.Append("  </url>\n");
        }

        // Statik sahifalar
        AddUrl("/", null, "daily", "1.0");
        AddUrl("/catalog", null, "daily", "0.9");
        foreach (var path in new[] { "/about", "/contact", "/delivery", "/services", "/branches", "/brands" })
        {
            AddUrl(path, null, "monthly", "0.3");
        }

        // Kategoriya va subkategoriya (katalog filtri) sahifalari
        foreach (var c in categories)
        {
            AddUrl($"/catalog?category={c.Slug}", null, "daily", "0.7");
            foreach (var s in c.Subcategories.OrderBy(s => s.Order))
            {
                AddUrl($"/catalog?category={c.Slug}&subcategory={s.Slug}", null, "daily", "0.6");
            }
        }

        // Har bir tasdiqlangan va hozir Odoo'da nashr etilgan mahsulot
        foreach (var p in products)
        {
            AddUrl($"/catalog/{p.Id}", p.UpdatedAt, "weekly", "0.8");
        }

        sb.Append("</urlset>");

        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }
}
