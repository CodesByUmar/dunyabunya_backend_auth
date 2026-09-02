using AuthApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthApi.Controllers;

// Admin panel uchun — rus tilidagi matnni Gemini orqali o'zbekchaga TAKLIF
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

        var suggestion = await _translation.SuggestUzTranslationAsync(dto.Text);
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
}
