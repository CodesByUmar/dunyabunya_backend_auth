using System.Text;

namespace AuthApi.Services;

// Kategoriya/subkategoriya nomidan (asosan ruscha) URL-uchun "slug" avtomatik
// yasaydi (masalan "Цемент и смеси" -> "tsement-i-smesi"). Admin panelda buni
// endi qo'lda kiritish shart emas.
public static class CategorySlugHelper
{
    private static readonly Dictionary<char, string> Translit = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d",
        ['е'] = "e", ['ё'] = "yo", ['ж'] = "zh", ['з'] = "z", ['и'] = "i",
        ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n",
        ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t",
        ['у'] = "u", ['ф'] = "f", ['х'] = "kh", ['ц'] = "ts", ['ч'] = "ch",
        ['ш'] = "sh", ['щ'] = "sch", ['ъ'] = "", ['ы'] = "y", ['ь'] = "",
        ['э'] = "e", ['ю'] = "yu", ['я'] = "ya"
    };

    /// <summary>Matndan (kiril/lotin aralash bo'lishi mumkin) kichik harfli, faqat
    /// lotin harf/raqam/chiziqchadan iborat slug yasaydi.</summary>
    public static string Slugify(string text)
    {
        var sb = new StringBuilder();
        foreach (var ch in text.ToLowerInvariant())
        {
            if (Translit.TryGetValue(ch, out var replacement))
            {
                sb.Append(replacement);
            }
            else if (char.IsLetterOrDigit(ch) && ch < 128)
            {
                sb.Append(ch);
            }
            else
            {
                sb.Append('-');
            }
        }

        // Ketma-ket chiziqchalarni birlashtirish, boshi/oxiridagini kesish.
        var slug = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "kategoriya" : slug;
    }

    /// <summary>Bir xil slug allaqachon bandligini hisobga olib (existingSlugs
    /// ichida), kerak bo'lsa "-2", "-3" ... qo'shib, noyob slug qaytaradi.</summary>
    public static string MakeUnique(string baseSlug, ISet<string> existingSlugs)
    {
        if (!existingSlugs.Contains(baseSlug)) return baseSlug;

        var i = 2;
        while (existingSlugs.Contains($"{baseSlug}-{i}")) i++;
        return $"{baseSlug}-{i}";
    }
}
