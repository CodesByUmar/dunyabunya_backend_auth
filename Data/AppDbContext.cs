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
    public DbSet<Category> Categories { get; set; }
    public DbSet<Subcategory> Subcategories { get; set; }
    public DbSet<Service> Services { get; set; }
    public DbSet<Advantage> Advantages { get; set; }
    public DbSet<Stat> Stats { get; set; }
    public DbSet<Partner> Partners { get; set; }
    public DbSet<Banner> Banners { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<ServiceReview> ServiceReviews { get; set; }
    public DbSet<ContactMessage> ContactMessages { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }

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
            entity.Property(p => p.Price).HasColumnType("numeric(18,2)");
            entity.Property(p => p.Cost).HasColumnType("numeric(18,2)");
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

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(n => n.UserId);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(o => o.UserId);
            entity.Property(o => o.Total).HasColumnType("numeric(18,2)");
            entity.HasMany(o => o.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.Property(i => i.Price).HasColumnType("numeric(18,2)");
        });
    }
}