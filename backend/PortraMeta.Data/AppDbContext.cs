using Microsoft.EntityFrameworkCore;
using PortraMeta.Data.Entities;

namespace PortraMeta.Data;

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

        modelBuilder.Entity<VideoFile>(entity =>
        {
            entity.HasIndex(v => v.FilePath).IsUnique();
            entity.Property(v => v.FileName).UseCollation("NOCASE");
            entity.Property(v => v.Title).UseCollation("NOCASE");
            entity.Property(v => v.OriginalTitle).UseCollation("NOCASE");
            entity.Property(v => v.Plot).UseCollation("NOCASE");
        });

        modelBuilder.Entity<Studio>(entity =>
        {
            entity.HasIndex(s => s.Name).IsUnique();
            entity.Property(s => s.Name).UseCollation("NOCASE");
        });

        modelBuilder.Entity<ExcludedFolder>()
            .HasIndex(e => new { e.LibraryId, e.Path })
            .IsUnique();
    }
}
