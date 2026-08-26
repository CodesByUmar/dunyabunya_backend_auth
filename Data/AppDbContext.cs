using Microsoft.EntityFrameworkCore;
using AuthApi.Models;

namespace AuthApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductImage> ProductImages { get; set; }
    public DbSet<ProductSpecification> ProductSpecifications { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Subcategory> Subcategories { get; set; }
    public DbSet<Service> Services { get; set; }
    public DbSet<Advantage> Advantages { get; set; }
    public DbSet<Stat> Stats { get; set; }
    public DbSet<Partner> Partners { get; set; }
    public DbSet<Banner> Banners { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<ReviewVote> ReviewVotes { get; set; }
    public DbSet<ServiceReview> ServiceReviews { get; set; }
    public DbSet<ContactMessage> ContactMessages { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<GiftTier> GiftTiers { get; set; }
    public DbSet<UserPoints> UserPoints { get; set; }
    public DbSet<GiftCampaign> GiftCampaigns { get; set; }
    public DbSet<UserGiftClaim> UserGiftClaims { get; set; }
    public DbSet<WishlistItem> WishlistItems { get; set; }
    public DbSet<Property> Properties { get; set; }
    public DbSet<Coupon> Coupons { get; set; }
    public DbSet<CouponRedemption> CouponRedemptions { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Email har doim normalize qilingan (kichik harf) holda saqlanadi,
        // shuning uchun unique index xavfsiz.
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();

            // Google orqali ro'yxatdan o'tgan userlar PhoneNumber = "" ga ega,
            // shuning uchun bo'sh qatorlarni indexdan chiqarib tashlaymiz (filtered index).
            entity.HasIndex(u => u.PhoneNumber)
                .IsUnique()
                .HasFilter("\"PhoneNumber\" <> ''");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(p => p.OdooProductId).IsUnique();
            entity.HasIndex(p => p.OdooTemplateId);
            entity.HasIndex(p => p.ApprovalStatus);
            entity.Property(p => p.Price).HasColumnType("numeric(18,2)");
            entity.Property(p => p.Cost).HasColumnType("numeric(18,2)");
            entity.HasMany(p => p.Images)
                .WithOne(i => i.Product)
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(p => p.Specifications)
                .WithOne(s => s.Product)
                .HasForeignKey(s => s.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(c => c.Slug).IsUnique();
            entity.HasMany(c => c.Subcategories)
                .WithOne(s => s.Category)
                .HasForeignKey(s => s.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Subcategory>(entity =>
        {
            entity.HasIndex(s => new { s.CategoryId, s.Slug }).IsUnique();
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasIndex(r => r.ProductId);
        });

        // Bitta foydalanuvchi bitta sharhga faqat bitta ovoz bera oladi —
        // bazadagi ushbu UNIQUE cheklov bo'lmasa, tezkor ketma-ket bosishlar
        // (yoki poyga holati) bir xil userdan bir nechta ovoz yozib qo'yishi mumkin edi.
        modelBuilder.Entity<ReviewVote>(entity =>
        {
            entity.HasIndex(v => new { v.ReviewId, v.UserId }).IsUnique();
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(n => n.UserId);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(o => o.UserId);
            entity.Property(o => o.Total).HasColumnType("numeric(18,2)");
            entity.Property(o => o.DiscountAmount).HasColumnType("numeric(18,2)");
            entity.HasMany(o => o.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.Property(i => i.Price).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<UserPoints>(entity =>
        {
            entity.HasIndex(p => p.UserId).IsUnique();
        });

        modelBuilder.Entity<UserGiftClaim>(entity =>
        {
            entity.HasIndex(c => c.UserId);
        });

        // Bitta foydalanuvchi bitta mahsulotni sevimlilarga faqat bir marta
        // qo'sha oladi — takroriy yozuvlarning oldini olish uchun.
        modelBuilder.Entity<WishlistItem>(entity =>
        {
            entity.HasIndex(w => new { w.UserId, w.ProductId }).IsUnique();
        });

        modelBuilder.Entity<Property>(entity =>
        {
            entity.HasIndex(p => p.UserId);
        });

        modelBuilder.Entity<Coupon>(entity =>
        {
            entity.HasIndex(c => c.Code).IsUnique();
            entity.Property(c => c.DiscountValue).HasColumnType("numeric(18,2)");
            entity.Property(c => c.MaxDiscountAmount).HasColumnType("numeric(18,2)");
            entity.Property(c => c.MinOrderAmount).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<CouponRedemption>(entity =>
        {
            entity.HasIndex(r => new { r.CouponId, r.UserId });
            entity.Property(r => r.DiscountAmount).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasIndex(m => m.ConversationId);
        });
    }
}