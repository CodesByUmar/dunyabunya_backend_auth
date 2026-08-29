using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryBilingualNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mavjud "Name" ustuni ruscha matn edi (masalan "Строительные материалы") —
            // shuning uchun NameRu'ga o'zgartiriladi (ma'lumot yo'qolmaydi).
            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Subcategories",
                newName: "NameRu");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Categories",
                newName: "NameRu");

            migrationBuilder.AddColumn<string>(
                name: "NameUz",
                table: "Subcategories",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameUz",
                table: "Categories",
                type: "text",
                nullable: false,
                defaultValue: "");

            // NameUz bo'sh qolmasin — admin tarjima qilib chiqqunicha, boshlang'ich
            // qiymat sifatida ruscha nomni ko'rsatib turadi (frontend "bo'sh" holatga
            // tushmasin).
            migrationBuilder.Sql(@"UPDATE ""Categories"" SET ""NameUz"" = ""NameRu"";");
            migrationBuilder.Sql(@"UPDATE ""Subcategories"" SET ""NameUz"" = ""NameRu"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameUz",
                table: "Subcategories");

            migrationBuilder.DropColumn(
                name: "NameUz",
                table: "Categories");

            migrationBuilder.RenameColumn(
                name: "NameRu",
                table: "Subcategories",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "NameRu",
                table: "Categories",
                newName: "Name");
        }
    }
}
