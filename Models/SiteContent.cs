namespace AuthApi.Models;

// Katalog kategoriyasi (masalan "Qurilish materiallari"), ichida subkategoriyalar.
// MUHIM: O'zbekcha nom bu yerda SAQLANMAYDI — u Translations jadvalidan
// ("data.categories.{Slug}" kaliti) olinadi, chunki xuddi shu tarjima
// frontendning boshqa joylarida (masalan bosh sahifa) ham ishlatiladi —
// ikkita alohida (bir-biridan uzilib qolishi mumkin bo'lgan) manba
// bo'lmasligi uchun (CategoriesController shu yerda birlashtiradi).
public class Category
{
    public int Id { get; set; }
    public string NameRu { get; set; } = string.Empty;
    // URL uchun — admin panelda qo'lda kiritilmaydi, nomdan avtomatik
    // yaratiladi (CategorySlugHelper) va keyingi tahrirlarda o'zgarmay qoladi
    // (mavjud havolalar/bannerlar buzilmasin).
    public string Slug { get; set; } = string.Empty;
    public string? Image { get; set; }
    public int Order { get; set; }
    public List<Subcategory> Subcategories { get; set; } = new();
}

public class Subcategory
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string NameRu { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Image { get; set; }
    public int Order { get; set; }
}

// Bosh sahifadagi "Xizmatlar" bloki (masalan "Yetkazib berish").
public class Service
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int Order { get; set; }
}

// Bosh sahifadagi "Afzalliklar" bloki.
public class Advantage
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int Order { get; set; }
}

// Bosh sahifadagi statistika (masalan "30 000+ Tovarlar").
public class Stat
{
    public int Id { get; set; }
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Order { get; set; }
}

// Hamkor/brend nomi (bosh sahifada logotiplar qatorida).
public class Partner
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
}

// Bosh sahifa banneri (slayder yoki yon kartochka).
public class Banner
{
    public int Id { get; set; }
    public string Position { get; set; } = "slider"; // "slider" | "side"
    public int Order { get; set; }
    public bool IsActive { get; set; } = true;

    public string TagRu { get; set; } = string.Empty;
    public string TagUz { get; set; } = string.Empty;
    public string TitleRu { get; set; } = string.Empty;
    public string TitleUz { get; set; } = string.Empty;
    public string SubtitleRu { get; set; } = string.Empty;
    public string SubtitleUz { get; set; } = string.Empty;
    public string ButtonTextRu { get; set; } = string.Empty;
    public string ButtonTextUz { get; set; } = string.Empty;

    public string Image { get; set; } = string.Empty; // URL yoki base64 data URI

    public string LinkType { get; set; } = "page"; // "page" | "category" | "subcategory"
    public string? PagePath { get; set; }
    public string? CategorySlug { get; set; }
    public string? SubcategorySlug { get; set; }

    public string AccentMode { get; set; } = "preset"; // "preset" | "custom"
    public string? Accent { get; set; }
    public string? CustomAccent { get; set; }
    public int OverlayOpacity { get; set; } = 60;
    public string TextAlign { get; set; } = "left";
    public string ImagePosition { get; set; } = "center";
}
