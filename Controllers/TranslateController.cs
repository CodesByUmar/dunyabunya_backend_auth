using AuthApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthApi.Controllers;

// Admin panel uchun — matnni Gemini orqali RU<->UZ (ikkala yo'nalishda) TAKLIF
// sifatida tarjima qiladi (Categories, mahsulot tavsifi/xususiyatlari kabi
// istalgan RU/UZ formada ishlatilishi mumkin). Admin har doim ko'rib chiqib,
// xohlasa tuzatib saqlaydi — bu yerda hech narsa avtomatik yozilmaydi.
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Superuser")]
public class TranslateController : ControllerBase
{
    private readonly ITranslationService _translation;

    public TranslateController(ITranslationService translation)
    {
        _translation = translation;
    }

    [HttpPost("suggest")]
    public async Task<IActionResult> Suggest(SuggestTranslationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Text))
        {
            return BadRequest(new { message = "Matn bo'sh bo'lishi mumkin emas." });
        }

        var targetLang = string.IsNullOrWhiteSpace(dto.TargetLang) ? "uz" : dto.TargetLang.Trim().ToLowerInvariant();
        if (targetLang != "ru" && targetLang != "uz")
        {
            return BadRequest(new { message = "targetLang faqat \"ru\" yoki \"uz\" bo'lishi mumkin." });
        }

        var suggestion = await _translation.SuggestTranslationAsync(dto.Text, targetLang);
        if (suggestion == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Tarjima xizmati hozir javob bermadi. Qo'lda kiriting." });
        }

        return Ok(new { suggestion });
    }
}

public class SuggestTranslationDto
{
    public string Text { get; set; } = string.Empty;

    // "ru" yoki "uz" — qaysi tilga tarjima qilinsin. Berilmasa "uz" (eski
    // xatti-harakat bilan moslik uchun zaxira qiymat).
    public string? TargetLang { get; set; }
}
