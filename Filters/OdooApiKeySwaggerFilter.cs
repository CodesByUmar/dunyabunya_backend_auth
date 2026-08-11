using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AuthApi.Filters;

/// <summary>
/// [RequireOdooApiKey] bilan belgilangan endpointlarga Swagger UI'da "Authorize"
/// orqali X-Api-Key kiritish imkonini qo'shadi (aks holda Swagger'da custom
/// header uchun maydon chiqmaydi).
/// </summary>
public class OdooApiKeySwaggerFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasApiKeyRequirement =
            context.MethodInfo.DeclaringType?.GetCustomAttributes(typeof(RequireOdooApiKeyAttribute), true).Any() == true ||
            context.MethodInfo.GetCustomAttributes(typeof(RequireOdooApiKeyAttribute), true).Any();

        if (!hasApiKeyRequirement) return;

        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("ApiKey", context.Document)] = new List<string>()
        });
    }
}
