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
            entity.HasIndex(p => p.OdooTemplateId).IsUnique();
            entity.Property(p => p.Price).HasColumnType("numeric(18,2)");
            entity.Property(p => p.Cost).HasColumnType("numeric(18,2)");
        });
    }
}