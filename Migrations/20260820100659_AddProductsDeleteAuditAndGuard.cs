using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthApi.Migrations
{
    /// <summary>
    /// "Products" jadvali sababi noaniq holda to'liq bo'shab qolgan hodisalar
    /// (bir necha marta takrorlangan, sababi hali aniqlanmagan) tufayli
    /// qo'shilgan ikkita himoya vositasi:
    /// 1) ProductsDeleteAudit — har bir o'chirishni (vaqt, DB foydalanuvchisi,
    ///    ulanish manzili bilan) yozib boradi, keyingi hodisani tekshirish uchun.
    /// 2) trg_products_guard_delete — bitta tranzaksiyada 50 tadan ortiq
    ///    mahsulot o'chirilishga urinilsa, BUTUNLAY BLOKLAYDI (tranzaksiya
    ///    qaytariladi) — shu bilan butun katalogning bir zumda yo'qolib
    ///    qolishining oldini oladi, kim/nima buni qilayotganidan qat'iy nazar.
    /// </summary>
    public partial class AddProductsDeleteAuditAndGuard : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "ProductsDeleteAudit" (
                  "AuditId" bigserial PRIMARY KEY,
                  "DeletedAt" timestamptz NOT NULL DEFAULT now(),
                  "DbUser" text,
                  "ApplicationName" text,
                  "ClientAddr" text,
                  "ProductId" integer,
                  "OdooProductId" integer,
                  "ProductName" text
                );
                """);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION log_products_delete() RETURNS trigger AS $$
                BEGIN
                  INSERT INTO "ProductsDeleteAudit" ("DbUser", "ApplicationName", "ClientAddr", "ProductId", "OdooProductId", "ProductName")
                  VALUES (current_user, current_setting('application_name', true), inet_client_addr()::text, OLD."Id", OLD."OdooProductId", OLD."Name");
                  RETURN OLD;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_products_delete_audit ON \"Products\";");
            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_products_delete_audit
                AFTER DELETE ON "Products"
                FOR EACH ROW EXECUTE FUNCTION log_products_delete();
                """);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION guard_products_bulk_delete() RETURNS trigger AS $$
                DECLARE
                  cnt integer;
                BEGIN
                  cnt := COALESCE(NULLIF(current_setting('dunyabunya.products_delete_count', true), ''), '0')::integer + 1;
                  PERFORM set_config('dunyabunya.products_delete_count', cnt::text, true);
                  IF cnt > 50 THEN
                    RAISE EXCEPTION 'XAVFSIZLIK BLOKI: bitta tranzaksiyada 50 tadan ortiq mahsulot o''chirilishi bloklandi (% ta urinildi). Agar bu ataylab, haqiqiy tozalash bo''lsa, avval shu trigger''ni o''chiring.', cnt;
                  END IF;
                  RETURN OLD;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_products_guard_delete ON \"Products\";");
            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_products_guard_delete
                BEFORE DELETE ON "Products"
                FOR EACH ROW EXECUTE FUNCTION guard_products_bulk_delete();
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_products_guard_delete ON \"Products\";");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS guard_products_bulk_delete();");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_products_delete_audit ON \"Products\";");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS log_products_delete();");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"ProductsDeleteAudit\";");
        }
    }
}
