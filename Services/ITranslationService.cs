namespace AuthApi.Services;

public interface ITranslationService
{
    Task<string?> SuggestUzTranslationAsync(string ruText);
}
