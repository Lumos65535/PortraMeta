using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PortraMeta.Core.Interfaces;
using PortraMeta.Core.Models;
using PortraMeta.Data.Entities;
using PortraMeta.Data.Utilities;

namespace PortraMeta.Data.Services;

public class LibraryService(
    AppDbContext db,
    FileSystemScanner scanner,
    INfoParser nfoParser,
    ILogger<LibraryService> logger) : ILibraryService
{
    public async Task<Result<IReadOnlyList<LibraryDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var libs = await db.Libraries
            .OrderBy(l => l.Name)
            .Select(l => new LibraryDto(l.Id, l.Name, l.Path, l.CreatedAt))
            .ToListAsync(ct);
        return Result<IReadOnlyList<LibraryDto>>.Ok(libs);
    }

    public async Task<Result<LibraryDto>> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var lib = await db.Libraries.FindAsync([id], ct);
        if (lib is null) return Result<LibraryDto>.Fail("Library not found");
        return Result<LibraryDto>.Ok(new LibraryDto(lib.Id, lib.Name, lib.Path, lib.CreatedAt));
    }

    public async Task<Result<LibraryDto>> CreateAsync(CreateLibraryRequest request, CancellationToken ct = default)
    {
        if (!Directory.Exists(request.Path))
            return Result<LibraryDto>.Fail($"Path does not exist: {request.Path}");

        var lib = new Library { Name = request.Name, Path = request.Path };
        db.Libraries.Add(lib);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Library created: {Name} at {Path}", lib.Name, lib.Path);
        return Result<LibraryDto>.Ok(new LibraryDto(lib.Id, lib.Name, lib.Path, lib.CreatedAt));
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var lib = await db.Libraries.FindAsync([id], ct);
        if (lib is null) return Result.Fail("Library not found");
        db.Libraries.Remove(lib);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Library deleted: {Id} ({Name})", id, lib.Name);
        return Result.Ok();
    }

    public async Task<Result<IReadOnlyList<string>>> GetSubdirectoriesAsync(int id, CancellationToken ct = default)
    {
        var lib = await db.Libraries.FindAsync([id], ct);
        if (lib is null) return Result<IReadOnlyList<string>>.Fail("Library not found");
        if (!Directory.Exists(lib.Path))
            return Result<IReadOnlyList<string>>.Fail($"Library path not found: {lib.Path}");

        var subdirs = scanner.GetSubdirectories(lib.Path);
        return Result<IReadOnlyList<string>>.Ok(subdirs);
    }

    public async Task<Result<IReadOnlyList<string>>> GetExcludedFoldersAsync(int id, CancellationToken ct = default)
    {
        var lib = await db.Libraries.FindAsync([id], ct);
        if (lib is null) return Result<IReadOnlyList<string>>.Fail("Library not found");

        var paths = await db.ExcludedFolders
            .Where(e => e.LibraryId == id)
            .Select(e => e.Path)
            .OrderBy(p => p)
            .ToListAsync(ct);

        return Result<IReadOnlyList<string>>.Ok(paths);
    }

    public async Task<Result> SetExcludedFoldersAsync(int id, IReadOnlyList<string> paths, CancellationToken ct = default)
    {
        var lib = await db.Libraries.FindAsync([id], ct);
        if (lib is null) return Result.Fail("Library not found");

        // Remove all existing exclusions for this library, then insert new ones
        var existing = await db.ExcludedFolders.Where(e => e.LibraryId == id).ToListAsync(ct);
        db.ExcludedFolders.RemoveRange(existing);

        var normalizedPaths = paths
            .Select(p => Path.GetFullPath(p))
            .Distinct()
            .ToList();

        // Validate all paths are within the library root to prevent path traversal
        var invalidPaths = normalizedPaths
            .Where(p => !FileSystemScanner.IsPathWithinBoundary(lib.Path, p))
            .ToList();
        if (invalidPaths.Count > 0)
            return Result.Fail($"Paths must be within the library directory: {string.Join(", ", invalidPaths)}");

        var normalized = normalizedPaths
            .Select(p => new ExcludedFolder { LibraryId = id, Path = p });

        await db.ExcludedFolders.AddRangeAsync(normalized, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Excluded folders updated: library {Id}, count={Count}", id, paths.Count);
        return Result.Ok();
    }

    public async Task<Result<ScanResultDto>> ScanAsync(int id, CancellationToken ct = default)
    {
        var library = await db.Libraries.FindAsync([id], ct);
        if (library is null)
            return Result<ScanResultDto>.Fail("Library not found");

        if (!Directory.Exists(library.Path))
            return Result<ScanResultDto>.Fail($"Library path not found: {library.Path}");

        logger.LogInformation("Scan started: library {Id} ({Name}) at {Path}", id, library.Name, library.Path);

        try
        {
            // Load excluded paths for this library
            var excludedPaths = await db.ExcludedFolders
                .Where(e => e.LibraryId == id)
                .Select(e => e.Path)
                .ToHashSetAsync(ct);

            var videoFiles = (await scanner.FindVideoFilesRecursiveAsync(library.Path, excludedPaths, ct)).ToList();

            // Remove any DB entries whose file path falls under an excluded directory
            if (excludedPaths.Count > 0)
            {
                var toRemove = await db.VideoFiles
                    .Where(v => v.LibraryId == id)
                    .ToListAsync(ct);

                var staleEntries = toRemove.Where(v =>
                    excludedPaths.Any(ex =>
                        v.FilePath.StartsWith(ex + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                        || v.FilePath.StartsWith(ex + '/', StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (staleEntries.Count > 0)
                {
                    db.VideoFiles.RemoveRange(staleEntries);
                    logger.LogInformation("Removed {Count} stale entries from excluded folders", staleEntries.Count);
                }
            }

            // Load full entities (with actors) for existing files so we can update them
            var existingFiles = await db.VideoFiles
                .Where(v => v.LibraryId == id)
                .Include(v => v.VideoActors)
                .ToDictionaryAsync(v => v.FilePath, ct);

            var studioCache = await db.Studios.ToDictionaryAsync(s => s.Name, ct);
            var actorCache = await db.Actors.ToDictionaryAsync(a => a.Name, ct);

            var newEntities = new List<VideoFile>();
            int updated = 0;
            int skipped = 0;
            int nfoParsed = 0;

            foreach (var file in videoFiles)
            {
                ct.ThrowIfCancellationRequested();

                var fullPath = Path.GetFullPath(file.FullName);
                bool hasNfo = scanner.HasNfoFile(fullPath);
                bool hasPoster = scanner.HasPosterFile(fullPath);
                bool hasFanart = scanner.HasFanartFile(fullPath);

                if (existingFiles.TryGetValue(fullPath, out var existing))
                {
                    // Re-check file flags; parse NFO if it was missing or metadata not yet populated
                    bool needsNfoParse = hasNfo && (!existing.HasNfo || existing.Title is null);
                    bool flagChanged = existing.HasNfo != hasNfo
                        || existing.HasPoster != hasPoster
                        || existing.HasFanart != hasFanart;

                    existing.HasNfo = hasNfo;
                    existing.HasPoster = hasPoster;
                    existing.HasFanart = hasFanart;
                    existing.FileModifiedAt ??= file.LastWriteTimeUtc;

                    if (needsNfoParse)
                    {
                        var nfoData = await nfoParser.ParseAsync(FileSystemScanner.NfoPath(fullPath), ct);
                        if (nfoData is not null)
                        {
                            existing.Title = nfoData.Title;
                            existing.OriginalTitle = nfoData.OriginalTitle;
                            existing.Year = nfoData.Year;
                            existing.Plot = nfoData.Plot;
                            existing.NfoUpdatedAt = DateTime.UtcNow;
                            MapExtendedNfoFields(nfoData, existing);
                            nfoParsed++;

                            if (nfoData.Studio is not null)
                            {
                                if (!studioCache.TryGetValue(nfoData.Studio, out var studio))
                                {
                                    studio = new Studio { Name = nfoData.Studio };
                                    db.Studios.Add(studio);
                                    studioCache[nfoData.Studio] = studio;
                                }
                                existing.Studio = studio;
                            }

                            // Add actors only if none exist yet (avoid duplicates on rescan)
                            if (!existing.VideoActors.Any())
                            {
                                foreach (var nfoActor in nfoData.Actors)
                                {
                                    if (!actorCache.TryGetValue(nfoActor.Name, out var actor))
                                    {
                                        actor = new Actor { Name = nfoActor.Name };
                                        db.Actors.Add(actor);
                                        actorCache[nfoActor.Name] = actor;
                                    }
                                    existing.VideoActors.Add(new VideoActor
                                    {
                                        Actor = actor,
                                        Role = nfoActor.Role,
                                        Order = nfoActor.Order
                                    });
                                }
                            }
                        }
                    }

                    if (flagChanged || needsNfoParse)
                        updated++;
                    else
                        skipped++;

                    continue;
                }

                // New file — insert
                var videoFile = new VideoFile
                {
                    LibraryId = id,
                    FileName = Path.GetFileName(fullPath),
                    FilePath = fullPath,
                    FileSizeBytes = file.Length,
                    HasNfo = hasNfo,
                    HasPoster = hasPoster,
                    HasFanart = hasFanart,
                    ScannedAt = DateTime.UtcNow,
                    FileModifiedAt = file.LastWriteTimeUtc
                };

                if (videoFile.HasNfo)
                {
                    var nfoData = await nfoParser.ParseAsync(FileSystemScanner.NfoPath(fullPath), ct);
                    if (nfoData is not null)
                    {
                        videoFile.Title = nfoData.Title;
                        videoFile.OriginalTitle = nfoData.OriginalTitle;
                        videoFile.Year = nfoData.Year;
                        videoFile.Plot = nfoData.Plot;
                        videoFile.NfoUpdatedAt = DateTime.UtcNow;
                        MapExtendedNfoFields(nfoData, videoFile);
                        nfoParsed++;

                        if (nfoData.Studio is not null)
                        {
                            if (!studioCache.TryGetValue(nfoData.Studio, out var studio))
                            {
                                studio = new Studio { Name = nfoData.Studio };
                                db.Studios.Add(studio);
                                studioCache[nfoData.Studio] = studio;
                            }
                            videoFile.Studio = studio;
                        }

                        foreach (var nfoActor in nfoData.Actors)
                        {
                            if (!actorCache.TryGetValue(nfoActor.Name, out var actor))
                            {
                                actor = new Actor { Name = nfoActor.Name };
                                db.Actors.Add(actor);
                                actorCache[nfoActor.Name] = actor;
                            }
                            videoFile.VideoActors.Add(new VideoActor
                            {
                                Actor = actor,
                                Role = nfoActor.Role,
                                Order = nfoActor.Order
                            });
                        }
                    }
                }

                newEntities.Add(videoFile);
            }

            if (newEntities.Count > 0)
                await db.VideoFiles.AddRangeAsync(newEntities, ct);

            await db.SaveChangesAsync(ct);

            var scanResult = new ScanResultDto(videoFiles.Count, newEntities.Count, updated, skipped, nfoParsed, excludedPaths.Count);
            logger.LogInformation("Scan completed: library {Id} — total {Total}, added {Added}, updated {Updated}, skipped {Skipped}, NFO parsed {NfoParsed}, excluded {Excluded} folder(s)",
                id, scanResult.Total, scanResult.Added, scanResult.Updated, scanResult.Skipped, scanResult.NfoParsed, scanResult.ExcludedFolders);
            return Result<ScanResultDto>.Ok(scanResult);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Scan cancelled: library {Id}", id);
            return Result<ScanResultDto>.Fail("Scan cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scan failed: library {Id}", id);
            return Result<ScanResultDto>.Fail($"Scan failed: {ex.Message}");
        }
    }

    private static string? SerializeList(IReadOnlyList<string> list)
        => list.Count > 0 ? JsonSerializer.Serialize(list) : null;

    private static void MapExtendedNfoFields(NfoData nfo, VideoFile v)
    {
        v.DirectorsJson = SerializeList(nfo.Directors);
        v.GenresJson = SerializeList(nfo.Genres);
        v.Runtime = nfo.Runtime;
        v.Mpaa = nfo.Mpaa;
        v.Premiered = nfo.Premiered;
        v.RatingsJson = nfo.Ratings.Count > 0
            ? JsonSerializer.Serialize(nfo.Ratings.Select(r => new { r.Name, r.Value, r.Votes, r.Max }))
            : null;
        v.UserRating = nfo.UserRating;
        v.UniqueIdsJson = nfo.UniqueIds.Count > 0
            ? JsonSerializer.Serialize(nfo.UniqueIds.ToDictionary(u => u.Type, u => u.Value))
            : null;
        v.TagsJson = SerializeList(nfo.Tags);
        v.SortTitle = nfo.SortTitle;
        v.Outline = nfo.Outline;
        v.Tagline = nfo.Tagline;
        v.CreditsJson = SerializeList(nfo.Credits);
        v.CountriesJson = SerializeList(nfo.Countries);
        v.SetName = nfo.Set;
        v.DateAdded = nfo.DateAdded;
        v.Top250 = nfo.Top250;
    }
}
