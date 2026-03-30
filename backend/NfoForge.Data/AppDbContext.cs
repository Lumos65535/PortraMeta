using Microsoft.EntityFrameworkCore;
using NfoForge.Data.Entities;

namespace NfoForge.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Library> Libraries => Set<Library>();
    public DbSet<VideoFile> VideoFiles => Set<VideoFile>();
    public DbSet<Studio> Studios => Set<Studio>();
    public DbSet<Actor> Actors => Set<Actor>();
    public DbSet<VideoActor> VideoActors => Set<VideoActor>();
    public DbSet<ExcludedFolder> ExcludedFolders => Set<ExcludedFolder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VideoActor>()
            .HasKey(va => new { va.VideoFileId, va.ActorId });

        modelBuilder.Entity<VideoFile>()
            .HasIndex(v => v.FilePath)
            .IsUnique();

        modelBuilder.Entity<Studio>()
            .HasIndex(s => s.Name)
            .IsUnique();

        modelBuilder.Entity<ExcludedFolder>()
            .HasIndex(e => new { e.LibraryId, e.Path })
            .IsUnique();
    }
}
