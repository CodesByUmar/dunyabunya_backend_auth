using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuthApi.Filters;

/// <summary>
/// Admin panel bo'limlariga (masalan "banners", "reviews") ruxsatni tekshiradi:
/// Role="Admin" — har doim ruxsat; Role="Superuser" — faqat JWT'dagi "permissions"
/// claim'ida shu bo'lim kaliti bo'lsagina ruxsat. Frontend'dagi
/// admin/src/lib/session.ts'dagi canAccessSection bilan bir xil mantiq,
/// backend tomonida ham majburiy qilib qo'yiladi (frontendga ishonib qolinmaydi).
/// </summary>
public class RequireSectionAttribute : ActionFilterAttribute
{
    private readonly string _section;

    public RequireSectionAttribute(string section)
    {
        _section = section;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Avtorizatsiya talab qilinadi." });
            return;
        }

        if (user.IsInRole("Admin"))
        {
            base.OnActionExecuting(context);
            return;
        }

        if (user.IsInRole("Superuser"))
        {
            var permissions = user.FindFirstValue("permissions")?
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

            if (permissions.Contains(_section))
            {
                base.OnActionExecuting(context);
                return;
            }
        }

        context.Result = new ObjectResult(new { message = "Bu bo'limga ruxsatingiz yo'q." })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
