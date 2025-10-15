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

    // Add services to the container.
    builder.Services.AddHttpClient("CustomClient");

    builder.Services.AddSingleton<UsageCounter>();
    builder.Services.AddSingleton<GPTApiClient>();
    builder.Services.AddSingleton<GoogleSheetsService>();

    var supabaseClient = new SupabaseClient();
    supabaseClient.Init().GetAwaiter().GetResult();
    builder.Services.AddSingleton(supabaseClient);

    builder.Services.AddControllers();
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    app.UseCors("ReactPolicy");

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();

    app.UseDefaultFiles(); // enables index.html as the default
    app.UseStaticFiles();

    app.MapFallbackToFile("index.html"); // for React Router

    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine("Unhandled exception: " + ex);
    throw;
}