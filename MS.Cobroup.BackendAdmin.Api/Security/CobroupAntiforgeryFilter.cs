using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MS.Cobroup.BackendAdmin.Api.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ValidateCobroupAntiForgeryTokenAttribute : TypeFilterAttribute
{
    public ValidateCobroupAntiForgeryTokenAttribute() : base(typeof(CobroupAntiforgeryFilter))
    {
    }
}

public sealed class CobroupAntiforgeryFilter(
    IAntiforgery antiforgery,
    ILogger<CobroupAntiforgeryFilter> logger) : IAsyncAuthorizationFilter
{
    public const string ErrorHeaderName = "X-Cobroup-Error";
    public const string ErrorCode = "antiforgery_token_invalid";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException ex)
        {
            logger.LogWarning("Antiforgery validation failed: {Message}", ex.Message);

            context.HttpContext.Response.Headers[ErrorHeaderName] = ErrorCode;
            context.Result = new ObjectResult(new
            {
                error = ErrorCode,
                code = ErrorCode,
                message = "El token de protección antiforgery es inválido o ha expirado."
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }
    }
}
