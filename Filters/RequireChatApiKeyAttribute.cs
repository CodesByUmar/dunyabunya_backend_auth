using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuthApi.Filters;

/// <summary>
/// AI chatbot xizmatidan kelayotgan so'rovlarni himoyalaydi — "X-Api-Key" header
/// Chatbot:ApiKeyInbound qiymatiga mos kelishi shart. Foydalanuvchi JWT
/// autentifikatsiyasidan mustaqil, server-to-server himoya qatlami
/// (RequireOdooApiKeyAttribute bilan bir xil naqsh).
/// </summary>
public class RequireChatApiKeyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expectedKey = config["Chatbot:ApiKeyInbound"];

        if (string.IsNullOrEmpty(expectedKey))
        {
            context.Result = new ObjectResult(new { message = "Server sozlamasi to'liq emas (ApiKeyInbound)." })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out var providedKey) ||
            !SecureEquals(providedKey.ToString(), expectedKey))
        {
            context.Result = new UnauthorizedObjectResult(new { message = "API key noto'g'ri yoki yo'q." });
            return;
        }

        base.OnActionExecuting(context);
    }

    private static bool SecureEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        if (aBytes.Length != bBytes.Length) return false;
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
