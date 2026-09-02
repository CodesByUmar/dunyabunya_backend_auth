using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthApi.Migrations
{
    /// <inheritdoc />
    public partial class AddProductBilingualDescriptionAndSpecs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mavjud ma'lumot (Description/Key/Value) faqat o'zbekcha edi —
            // shuning uchun eski ustunlar *Uz'ga ko'chiriladi (yo'qolmaydi),
            // *Ru esa yangi, bo'sh ustun sifatida qo'shiladi.
            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Products",
                newName: "DescriptionUz");

            migrationBuilder.AddColumn<string>(
                name: "DescriptionRu",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.RenameColumn(
                name: "Key",
                table: "ProductSpecifications",
                newName: "KeyUz");

            migrationBuilder.RenameColumn(
                name: "Value",
                table: "ProductSpecifications",
                newName: "ValueUz");

            migrationBuilder.AddColumn<string>(
                name: "KeyRu",
                table: "ProductSpecifications",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ValueRu",
                table: "ProductSpecifications",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KeyRu",
                table: "ProductSpecifications");

            migrationBuilder.DropColumn(
                name: "ValueRu",
                table: "ProductSpecifications");

            migrationBuilder.RenameColumn(
                name: "KeyUz",
                table: "ProductSpecifications",
                newName: "Key");

            migrationBuilder.RenameColumn(
                name: "ValueUz",
                table: "ProductSpecifications",
                newName: "Value");

            migrationBuilder.DropColumn(
                name: "DescriptionRu",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "DescriptionUz",
                table: "Products",
                newName: "Description");
        }
    }
}
