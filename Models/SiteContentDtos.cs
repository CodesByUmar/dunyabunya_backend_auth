namespace AuthApi.Models;

// Id — mavjud subkategoriyani tahrirlashda beriladi (shunda uning eski Slug'i
// saqlanib qoladi); yangi subkategoriya qo'shilganda bo'sh qoldiriladi (yangi
// slug avtomatik yaratiladi). Slug bu yerda YO'Q — admin uni kiritmaydi.
public class SubcategoryDto
{
    public int? Id { get; set; }
    public string NameRu { get; set; } = string.Empty;
    public string NameUz { get; set; } = string.Empty;
    public string? Image { get; set; }
    public int Order { get; set; }
}

public class CategoryDto
{
    public string NameRu { get; set; } = string.Empty;
    public string NameUz { get; set; } = string.Empty;
    public string? Image { get; set; }
    public int Order { get; set; }
    public List<SubcategoryDto>? Subcategories { get; set; }
}

public class ServiceDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int Order { get; set; }
}

public class AdvantageDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int Order { get; set; }
}

public class StatDto
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Order { get; set; }
}

public class PartnerDto
{
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
}

public class BannerDto
{
    public string Position { get; set; } = "slider";
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
    public string Image { get; set; } = string.Empty;
    public string LinkType { get; set; } = "page";
    public string? PagePath { get; set; }
    public string? CategorySlug { get; set; }
    public string? SubcategorySlug { get; set; }
    public string AccentMode { get; set; } = "preset";
    public string? Accent { get; set; }
    public string? CustomAccent { get; set; }
    public int OverlayOpacity { get; set; } = 60;
    public string TextAlign { get; set; } = "left";
    public string ImagePosition { get; set; } = "center";
}
