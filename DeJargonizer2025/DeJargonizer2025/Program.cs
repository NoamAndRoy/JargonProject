using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
// using Microsoft.AspNetCore.HttpOverrides; // only if you need X-Forwarded-* later

using DeJargonizer2025.Helpers;
using JargonProject.Services;

try
{
    var builder = WebApplication.CreateBuilder(args);

    DotNetEnv.Env.Load();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ReactPolicy", policy =>
        {
            policy.WithOrigins("http://localhost:5174", "http://localhost:5173")
                  .WithMethods("GET", "POST", "PUT", "DELETE")
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });

    builder.Services.AddHttpClient("CustomClient");
    builder.Services.AddSingleton<UsageCounter>();
    builder.Services.AddSingleton<GPTApiClient>();
    builder.Services.AddSingleton<GoogleSheetsService>();

    var supabaseClient = new SupabaseClient();
    supabaseClient.Init().GetAwaiter().GetResult();
    builder.Services.AddSingleton(supabaseClient);

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();


    var supabaseSecret = Environment.GetEnvironmentVariable("SUPABASE_JWT_SECRET")
        ?? throw new InvalidOperationException("SUPABASE_JWT_SECRET is missing.");
    
    var projectRef = Environment.GetEnvironmentVariable("SUPABASE_PROJECT_REF"); 

    var issuer = string.IsNullOrWhiteSpace(projectRef)
        ? null
        : $"https://{projectRef}.supabase.co/auth/v1";

    if (!string.IsNullOrWhiteSpace(supabaseSecret))
    {
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.RequireHttpsMetadata = false; // container runs http
                o.SaveToken = true;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = !string.IsNullOrWhiteSpace(issuer),
                    ValidIssuer = issuer,
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(supabaseSecret)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
                o.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var auth = ctx.Request.Headers.Authorization.ToString();
                        if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                            return Task.CompletedTask;

                        var token = auth.Substring("Bearer ".Length).Trim();
                        if (string.IsNullOrWhiteSpace(token) || token == "null" || token == "undefined")
                            return Task.CompletedTask; // treat as anonymous (no IDX12741 spam)

                        ctx.Token = token;
                        return Task.CompletedTask;
                    }
                };
            });
    }

    var app = builder.Build();

    app.UseCors("ReactPolicy");

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // Serve your SPA/static files
    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.UseRouting();

    if (!string.IsNullOrWhiteSpace(supabaseSecret))
        app.UseAuthentication();

    app.UseAuthorization();

    app.MapControllers();

    app.MapFallbackToFile("index.html");

    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine("Unhandled exception: " + ex);
    throw;
}
