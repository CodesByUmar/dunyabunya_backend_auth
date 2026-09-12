using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthApi.Migrations
{
    /// <inheritdoc />
    public partial class AddProductIsOnline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOnline",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill: eski uch holatli ApprovalStatus'dan yangi ikki holatli
            // IsOnline'ga ko'chirish — faqat "approved" bo'lganlar Online bo'lib
            // qoladi ("pending" va "rejected" ikkalasi ham Offline'ga tushadi,
            // ular orasidagi farq endi kerak emas — admin qaytadan Online qilishi
            // mumkin). ApprovalStatus ustunining o'zi ataylab o'chirilmaydi.
            migrationBuilder.Sql(@"UPDATE ""Products"" SET ""IsOnline"" = true WHERE ""ApprovalStatus"" = 'approved';");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsOnline",
                table: "Products",
                column: "IsOnline");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_IsOnline",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsOnline",
                table: "Products");
        }
    }
}
