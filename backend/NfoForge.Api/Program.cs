using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using NfoForge.Core.Interfaces;
using NfoForge.Data;
using NfoForge.Data.Services;
using NfoForge.Data.Utilities;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var dbPath = builder.Configuration["Database:Path"] ?? "nfoforge.db";
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddScoped<ILibraryService, LibraryService>();
builder.Services.AddScoped<IVideoService, VideoService>();
builder.Services.AddScoped<INfoParser, NfoParser>();
builder.Services.AddScoped<INfoService, NfoService>();
builder.Services.AddSingleton<FileSystemScanner>();

// CORS — supports AllowAnyOrigin flag (for local/desktop mode) or a comma-separated
// list of allowed origins. Falls back to legacy "Cors:AllowedOrigin" for compatibility.
var corsSection = builder.Configuration.GetSection("Cors");
var allowAnyOrigin = corsSection.GetValue<bool>("AllowAnyOrigin");
var originsRaw = corsSection["AllowedOrigins"]
    ?? corsSection["AllowedOrigin"]
    ?? "http://localhost:3000";
var allowedOrigins = originsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
    {
        if (allowAnyOrigin)
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        else
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    }));

var app = builder.Build();

// Global exception handler — returns unified 500 JSON
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    var feature = ctx.Features.Get<IExceptionHandlerFeature>();
    var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
    if (feature?.Error is not null)
        logger.LogError(feature.Error, "Unhandled exception");

    ctx.Response.StatusCode = 500;
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsJsonAsync(new { success = false, error = "Internal server error" });
}));

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.UseCors();

// Optional API Key authentication — enabled only when "Auth:ApiKey" is non-empty.
// CORS preflight (OPTIONS) is exempted so browsers can negotiate origins without a key.
var apiKey = builder.Configuration["Auth:ApiKey"];
if (!string.IsNullOrWhiteSpace(apiKey))
{
    app.Use(async (ctx, next) =>
    {
        if (ctx.Request.Method != "OPTIONS"
            && (!ctx.Request.Headers.TryGetValue("X-Api-Key", out var providedKey)
                || providedKey != apiKey))
        {
            ctx.Response.StatusCode = 401;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsJsonAsync(new { success = false, error = "Unauthorized" });
            return;
        }
        await next();
    });
}

app.MapControllers();

app.Run();

// Make Program accessible for WebApplicationFactory in integration tests
public partial class Program { }
