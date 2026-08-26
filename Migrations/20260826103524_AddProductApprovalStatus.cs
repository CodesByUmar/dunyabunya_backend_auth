using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthApi.Migrations
{
    /// <inheritdoc />
    public partial class AddProductApprovalStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MUHIM: mavjud 300+ mahsulot allaqachon ochiq katalogda ko'rinib turibdi —
            // ularni birdaniga yashirib qo'ymaslik uchun standart qiymat "approved".
            // Faqat Odoo'dan BUNDAN KEYIN keladigan YANGI mahsulotlar "pending" bo'lib
            // qo'shiladi (ProductSyncBackgroundService orqali).
            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "Products",
                type: "text",
                nullable: false,
                defaultValue: "approved");

            migrationBuilder.CreateIndex(
                name: "IX_Products_ApprovalStatus",
                table: "Products",
                column: "ApprovalStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_ApprovalStatus",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "Products");
        }
    }
}
