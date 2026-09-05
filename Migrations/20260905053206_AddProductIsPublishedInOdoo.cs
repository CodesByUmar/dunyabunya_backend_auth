using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthApi.Migrations
{
    /// <inheritdoc />
    public partial class AddProductIsPublishedInOdoo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MUHIM: defaultValue=true (EF avtomatik false taklif qilgan edi) —
            // mavjud barcha mahsulotlar aynan is_published=true bo'lgani uchun
            // sinxronlangan, shuning uchun ularni "false" bilan boshlab, keyingi
            // sync siklini kutish o'rniga, haqiqatga mos "true" bilan backfill
            // qilamiz (aks holda butun katalog bir lahzaga yo'qolib qolardi).
            migrationBuilder.AddColumn<bool>(
                name: "IsPublishedInOdoo",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublishedInOdoo",
                table: "Products");
        }
    }
}
