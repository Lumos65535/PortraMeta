using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NfoForge.Core.Interfaces;
using NfoForge.Core.Models;
using NfoForge.Data.Entities;
using NfoForge.Data.Utilities;

namespace NfoForge.Data.Services;

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

        var normalized = paths
            .Select(p => Path.GetFullPath(p))
            .Distinct()
            .Select(p => new ExcludedFolder { LibraryId = id, Path = p });

        await db.ExcludedFolders.AddRangeAsync(normalized, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Excluded folders updated: library {Id}, count={Count}", id, paths.Count);
        return Result.Ok();
    }

    public async Task<Result<string>> ScanAsync(int id, CancellationToken ct = default)
    {
        var library = await db.Libraries.FindAsync([id], ct);
        if (library is null)
            return Result<string>.Fail("Library not found");

        if (!Directory.Exists(library.Path))
            return Result<string>.Fail($"Library path not found: {library.Path}");

        logger.LogInformation("Scan started: library {Id} ({Name}) at {Path}", id, library.Name, library.Path);

        try
        {
            // Load excluded paths for this library
            var excludedPaths = await db.ExcludedFolders
                .Where(e => e.LibraryId == id)
                .Select(e => e.Path)
                .ToHashSetAsync(ct);

            var videoFiles = (await scanner.FindVideoFilesRecursiveAsync(library.Path, excludedPaths, ct)).ToList();

            var existingPaths = await db.VideoFiles
                .Where(v => v.LibraryId == id)
                .Select(v => v.FilePath)
                .ToHashSetAsync(ct);

            var studioCache = await db.Studios.ToDictionaryAsync(s => s.Name, ct);
            var actorCache = await db.Actors.ToDictionaryAsync(a => a.Name, ct);

            var newEntities = new List<VideoFile>();
            int skipped = 0;
            int nfoParsed = 0;

            foreach (var file in videoFiles)
            {
                ct.ThrowIfCancellationRequested();

                var fullPath = Path.GetFullPath(file.FullName);

                if (existingPaths.Contains(fullPath))
                {
                    skipped++;
                    continue;
                }

                var videoFile = new VideoFile
                {
                    LibraryId = id,
                    FileName = Path.GetFileName(fullPath),
                    FilePath = fullPath,
                    FileSizeBytes = file.Length,
                    HasNfo = scanner.HasNfoFile(fullPath),
                    HasPoster = scanner.HasPosterFile(fullPath),
                    ScannedAt = DateTime.UtcNow
                };

                if (videoFile.HasNfo)
                {
                    var nfoPath = $"{fullPath}.nfo";
                    var nfoData = await nfoParser.ParseAsync(nfoPath, ct);
                    if (nfoData is not null)
                    {
                        videoFile.Title = nfoData.Title;
                        videoFile.OriginalTitle = nfoData.OriginalTitle;
                        videoFile.Year = nfoData.Year;
                        videoFile.Plot = nfoData.Plot;
                        videoFile.NfoUpdatedAt = DateTime.UtcNow;
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

            var summary = $"Scanned {videoFiles.Count} files: added {newEntities.Count} (NFO parsed: {nfoParsed}), skipped {skipped}";
            if (excludedPaths.Count > 0)
                summary += $", excluded {excludedPaths.Count} folder(s)";

            logger.LogInformation("Scan completed: library {Id} — {Summary}", id, summary);
            return Result<string>.Ok(summary);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Scan cancelled: library {Id}", id);
            return Result<string>.Fail("Scan cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scan failed: library {Id}", id);
            return Result<string>.Fail($"Scan failed: {ex.Message}");
        }
    }
}
