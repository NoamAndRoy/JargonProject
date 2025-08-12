using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

public static class HttpContextExtensions
{
    public static async Task<string?> TryGetUserIdAsync(this HttpContext ctx)
    {
        var result = await ctx.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        return result.Succeeded
            ? result.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            : null;
    }
}
