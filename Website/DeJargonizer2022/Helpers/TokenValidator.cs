using System;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Diagnostics;

public static class TokenValidator
{
    private static readonly string JwtSecret = "e6tShGcrL2+S0CkS3aC3fGt/A3Ss6Gk/oN9E65pOoMDOrMk0iltzwDg2xyYSOhAowMYNSZwzL4ZiCLR1t+8ZAg==";

    public static ClaimsPrincipal ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(JwtSecret));

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            IssuerSigningKey = key,
            ValidateLifetime = true
        };

        try
        {
            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            return principal;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Token validation error: {ex.Message}");
            return null;
        }
    }
}
