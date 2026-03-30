using Microsoft.EntityFrameworkCore;
using NfoForge.Core.Interfaces;
using NfoForge.Core.Models;
using NfoForge.Data.Entities;
using NfoForge.Data.Utilities;

namespace NfoForge.Data.Services;

public class LibraryService(AppDbContext db) : ILibraryService
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
        return Result<LibraryDto>.Ok(new LibraryDto(lib.Id, lib.Name, lib.Path, lib.CreatedAt));
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var lib = await db.Libraries.FindAsync([id], ct);
        if (lib is null) return Result.Fail("Library not found");
        db.Libraries.Remove(lib);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<string>> ScanAsync(int id, CancellationToken ct = default)
    {
        var library = await db.Libraries.FindAsync([id], ct);
        if (library is null)
            return Result<string>.Fail("Library not found");

        if (!Directory.Exists(library.Path))
            return Result<string>.Fail($"Library path not found: {library.Path}");

        try
        {
            var scanner = new FileSystemScanner();
            var videoFiles = await scanner.FindVideoFilesRecursiveAsync(library.Path, ct);

            // Get existing file paths to detect duplicates
            var existingPaths = await db.VideoFiles
                .Where(v => v.LibraryId == id)
                .Select(v => v.FilePath)
                .ToHashSetAsync(ct);

            var newEntities = new List<VideoFile>();
            int skipped = 0;

            foreach (var file in videoFiles)
            {
                ct.ThrowIfCancellationRequested();

                var fullPath = Path.GetFullPath(file.FullName);

                // Skip if this path already exists
                if (existingPaths.Contains(fullPath))
                {
                    skipped++;
                    continue;
                }

                newEntities.Add(new VideoFile
                {
                    LibraryId = id,
                    FileName = Path.GetFileName(fullPath),
                    FilePath = fullPath,
                    FileSizeBytes = file.Length,
                    HasNfo = scanner.HasNfoFile(fullPath),
                    HasPoster = scanner.HasPosterFile(fullPath),
                    ScannedAt = DateTime.UtcNow
                });
            }

            // Batch insert new video files
            if (newEntities.Count > 0)
                await db.VideoFiles.AddRangeAsync(newEntities, ct);

            await db.SaveChangesAsync(ct);

            var summary = $"Scanned {videoFiles.Count()} files: added {newEntities.Count}, skipped {skipped}";
            return Result<string>.Ok(summary);
        }
        catch (OperationCanceledException)
        {
            return Result<string>.Fail("Scan cancelled");
        }
        catch (Exception ex)
        {
            return Result<string>.Fail($"Scan failed: {ex.Message}");
        }
    }
}
