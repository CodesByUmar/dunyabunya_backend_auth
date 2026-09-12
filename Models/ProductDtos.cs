namespace AuthApi.Models;

public class UpdateProductDescriptionDto
{
    public string? DescriptionRu { get; set; }
    public string? DescriptionUz { get; set; }
}

// Narx (Price) BU YERDA YO'Q — admin panelda umuman tahrirlanmaydi.
// Category — ERKIN matn EMAS, faqat GET /api/products/category-options
// ro'yxatidagi qiymatlardan biri bo'lishi shart (aks holda frontendning
// kategoriya-filtrlash mantig'i mahsulotni "kategoriyasiz" qilib qo'yadi).
// Subcategory — erkin matn (frontend uni faqat ko'rsatish uchun ishlatadi,
// qattiq yozilgan ro'yxatga bog'liq emas).
public class UpdateProductDetailsDto
{
    public string? Name { get; set; }
    public string? NameUz { get; set; }
    public string? Category { get; set; }
    public string? Subcategory { get; set; }

    // Tahrirlash oynasidagi Online/Offline dropdown'i — berilsa, boshqa
    // maydonlar bilan bir vaqtda saqlanadi (alohida "Tasdiqlash" bosish
    // shart emas). Odoo'da is_published=false bo'lsa "true" bilan
    // yuborilsa BadRequest qaytadi (q. UpdateProductDetails).
    public bool? IsOnline { get; set; }
}

public class ProductSpecificationDto
{
    public string KeyRu { get; set; } = string.Empty;
    public string KeyUz { get; set; } = string.Empty;
    public string ValueRu { get; set; } = string.Empty;
    public string ValueUz { get; set; } = string.Empty;
}

// Ro'yxatdagi tezkor Online/Offline tugmasi uchun (to'liq tahrirlash
// oynasini ochmasdan) — q. ProductsController.SetOnlineStatus.
public class UpdateOnlineStatusDto
{
    public bool IsOnline { get; set; }
}
