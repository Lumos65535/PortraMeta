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

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(
                builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()));

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
app.MapControllers();

app.Run();
