namespace AuthApi.Services;

public interface ITranslationService
{
    /// <param name="text">Tarjima qilinadigan matn.</param>
    /// <param name="targetLang">Qaysi tilga tarjima qilinsin — "ru" yoki "uz".</param>
    Task<string?> SuggestTranslationAsync(string text, string targetLang);
}
