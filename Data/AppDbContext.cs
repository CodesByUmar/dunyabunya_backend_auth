using Microsoft.EntityFrameworkCore;
using AuthApi.Models;

namespace AuthApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }

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
    }
}