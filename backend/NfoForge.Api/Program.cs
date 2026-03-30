using Microsoft.EntityFrameworkCore;
using NfoForge.Core.Interfaces;
using NfoForge.Data.Services;
using NfoForge.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var dbPath = builder.Configuration["Database:Path"] ?? "nfoforge.db";
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddScoped<ILibraryService, LibraryService>();
builder.Services.AddScoped<IVideoService, VideoService>();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(
                builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()));

var app = builder.Build();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.UseCors();
app.MapControllers();

app.Run();
