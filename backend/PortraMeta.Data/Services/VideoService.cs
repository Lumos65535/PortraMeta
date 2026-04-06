using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PortraMeta.Core.Interfaces;
using PortraMeta.Core.Models;
using PortraMeta.Data.Entities;
using PortraMeta.Data.Utilities;

namespace PortraMeta.Data.Services;

public class VideoService(AppDbContext db, INfoService nfoService, ILogger<VideoService> logger) : IVideoService
{
    private const long MaxPosterBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedMimeTypes =
        ["image/jpeg", "image/jpg", "image/png", "image/webp"];

    public async Task<Result<PagedResult<VideoFileDto>>> GetAllAsync(
        VideoFileFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.VideoFiles.AsQueryable();

        if (filter.HasNfo.HasValue)
            query = query.Where(v => v.HasNfo == filter.HasNfo.Value);
        if (filter.HasPoster.HasValue)
            query = query.Where(v => v.HasPoster == filter.HasPoster.Value);
        if (filter.LibraryId.HasValue)
            query = query.Where(v => v.LibraryId == filter.LibraryId.Value);
        if (filter.StudioId.HasValue)
            query = query.Where(v => v.StudioId == filter.StudioId.Value);
        if (!string.IsNullOrEmpty(filter.Search))
        {
            var pattern = $"%{filter.Search}%";
            query = query.Where(v =>
                EF.Functions.Like(v.FileName, pattern) ||
                (v.Title != null && EF.Functions.Like(v.Title, pattern)) ||
                (v.OriginalTitle != null && EF.Functions.Like(v.OriginalTitle, pattern)) ||
                (v.Plot != null && EF.Functions.Like(v.Plot, pattern)) ||
                (v.Studio != null && EF.Functions.Like(v.Studio.Name, pattern)));
        }

        var total = await query.CountAsync(ct);

        IQueryable<VideoFile> sorted = (filter.SortBy, filter.SortDesc) switch
        {
            ("title", false)          => query.OrderBy(v => v.Title ?? v.FileName),
            ("title", true)           => query.OrderByDescending(v => v.Title ?? v.FileName),
            ("year", false)           => query.OrderBy(v => v.Year),
            ("year", true)            => query.OrderByDescending(v => v.Year),
            ("studioName", false)     => query.OrderBy(v => v.Studio != null ? v.Studio.Name : null),
            ("studioName", true)      => query.OrderByDescending(v => v.Studio != null ? v.Studio.Name : null),
            ("fileSizeBytes", false)  => query.OrderBy(v => v.FileSizeBytes),
            ("fileSizeBytes", true)   => query.OrderByDescending(v => v.FileSizeBytes),
            ("scannedAt", false)      => query.OrderBy(v => v.ScannedAt),
            ("scannedAt", true)       => query.OrderByDescending(v => v.ScannedAt),
            ("fileModifiedAt", false) => query.OrderBy(v => v.FileModifiedAt),
            ("fileModifiedAt", true)  => query.OrderByDescending(v => v.FileModifiedAt),
            ("originalTitle", false)  => query.OrderBy(v => v.OriginalTitle),
            ("originalTitle", true)   => query.OrderByDescending(v => v.OriginalTitle),
            ("fileName", true)        => query.OrderByDescending(v => v.FileName),
            _                         => query.OrderBy(v => v.FileName),
        };

        var items = await sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new VideoFileDto(
                v.Id, v.LibraryId, v.FileName, v.FilePath, v.FileSizeBytes,
                v.HasNfo, v.HasPoster, v.HasFanart, v.Title, v.OriginalTitle, v.Year, v.Plot,
                v.Studio != null ? v.Studio.Name : null, v.ScannedAt,
                null, v.FileModifiedAt,
                null, null, v.Runtime, v.Mpaa, v.Premiered,
                null, v.UserRating, null, null, v.SortTitle,
                null, null, null, null,
                null, null, null))
            .ToListAsync(ct);

        logger.LogDebug("GetAll videos: page={Page}, pageSize={PageSize}, total={Total}", page, pageSize, total);
        return Result<PagedResult<VideoFileDto>>.Ok(new PagedResult<VideoFileDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<VideoFileDto>> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var v = await db.VideoFiles
            .Include(v => v.Studio)
            .Include(v => v.VideoActors)
                .ThenInclude(va => va.Actor)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

        if (v is null)
        {
            logger.LogWarning("Video not found: {Id}", id);
            return Result<VideoFileDto>.Fail("Video not found");
        }

        return Result<VideoFileDto>.Ok(ToDto(v));
    }

    public async Task<Result<VideoFileDto>> UpdateAsync(int id, UpdateVideoRequest request, CancellationToken ct = default)
    {
        var v = await db.VideoFiles
            .Include(v => v.Studio)
            .Include(v => v.VideoActors)
                .ThenInclude(va => va.Actor)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

        if (v is null)
        {
            logger.LogWarning("Video not found for update: {Id}", id);
            return Result<VideoFileDto>.Fail("Video not found");
        }

        v.Title = request.Title;
        v.OriginalTitle = request.OriginalTitle;
        v.Year = request.Year;
        v.Plot = request.Plot;
        v.NfoUpdatedAt = DateTime.UtcNow;

        // Extended NFO fields
        v.DirectorsJson = SerializeList(request.Directors);
        v.GenresJson = SerializeList(request.Genres);
        v.Runtime = request.Runtime;
        v.Mpaa = request.Mpaa;
        v.Premiered = request.Premiered;
        v.RatingsJson = request.Ratings is { Count: > 0 }
            ? JsonSerializer.Serialize(request.Ratings)
            : null;
        v.UserRating = request.UserRating;
        v.UniqueIdsJson = request.UniqueIds is { Count: > 0 }
            ? JsonSerializer.Serialize(request.UniqueIds)
            : null;
        v.TagsJson = SerializeList(request.Tags);
        v.SortTitle = request.SortTitle;
        v.Outline = request.Outline;
        v.Tagline = request.Tagline;
        v.CreditsJson = SerializeList(request.Credits);
        v.CountriesJson = SerializeList(request.Countries);
        v.SetName = request.SetName;
        v.DateAdded = request.DateAdded;
        v.Top250 = request.Top250;

        if (request.StudioName is not null)
        {
            var studio = await db.Studios.FirstOrDefaultAsync(s => s.Name == request.StudioName, ct)
                ?? new Studio { Name = request.StudioName };
            if (studio.Id == 0) db.Studios.Add(studio);
            v.Studio = studio;
        }
        else
        {
            v.StudioId = null;
            v.Studio = null;
        }

        if (request.Actors is not null)
        {
            db.VideoActors.RemoveRange(v.VideoActors);
            await db.SaveChangesAsync(ct); // flush deletions before re-inserting
            v.VideoActors.Clear();

            foreach (var actorReq in request.Actors)
            {
                var actor = await db.Actors.FirstOrDefaultAsync(a => a.Name == actorReq.Name, ct)
                    ?? new Actor { Name = actorReq.Name };
                if (actor.Id == 0) db.Actors.Add(actor);
                v.VideoActors.Add(new VideoActor
                {
                    VideoFile = v,
                    Actor = actor,
                    Role = string.IsNullOrWhiteSpace(actorReq.Role) ? null : actorReq.Role,
                    Order = actorReq.Order,
                });
            }
        }

        await db.SaveChangesAsync(ct);

        var dto = ToDto(v);
        await nfoService.WriteAsync(FileSystemScanner.NfoPath(v.FilePath), dto, ct);

        if (!v.HasNfo)
        {
            v.HasNfo = true;
            await db.SaveChangesAsync(ct);
            dto = dto with { HasNfo = true };
        }

        logger.LogInformation("Video updated and NFO written: {Id} ({FileName})", id, v.FileName);
        return Result<VideoFileDto>.Ok(dto);
    }

    public async Task<Result<string>> GetPosterPathAsync(int id, CancellationToken ct = default)
    {
        var v = await db.VideoFiles.AsNoTracking()
            .Select(x => new { x.Id, x.FilePath })
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (v is null)
        {
            logger.LogWarning("Video not found: {Id}", id);
            return Result<string>.Fail("Video not found");
        }

        var posterPath = FindImageWithAnyExtension(v.FilePath, "-poster");
        if (posterPath is null)
            return Result<string>.Fail("Poster not found");

        return Result<string>.Ok(posterPath);
    }

    public async Task<Result<VideoFileDto>> UploadPosterAsync(
        int id, Stream imageStream, string contentType, long contentLength,
        CancellationToken ct = default)
    {
        var v = await db.VideoFiles
            .Include(x => x.Studio)
            .Include(x => x.VideoActors).ThenInclude(va => va.Actor)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (v is null)
        {
            logger.LogWarning("Video not found for poster upload: {Id}", id);
            return Result<VideoFileDto>.Fail("Video not found");
        }

        if (contentLength > 0 && contentLength > MaxPosterBytes)
            return Result<VideoFileDto>.Fail("File too large (max 10 MB)");

        if (!AllowedMimeTypes.Contains(contentType.ToLowerInvariant()))
            return Result<VideoFileDto>.Fail("Invalid file type. Only JPEG, PNG, and WebP are allowed");

        var destPath = FileSystemScanner.PosterPath(v.FilePath);
        try
        {
            await using var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await imageStream.CopyToAsync(fs, ct);
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "Failed to save poster for video {Id}", id);
            try { File.Delete(destPath); } catch { /* ignore cleanup errors */ }
            return Result<VideoFileDto>.Fail("Failed to save poster file");
        }

        v.HasPoster = true;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Poster uploaded for video {Id} ({FileName})", id, v.FileName);
        return Result<VideoFileDto>.Ok(ToDto(v));
    }

    public async Task<Result<string>> GetFanartPathAsync(int id, CancellationToken ct = default)
    {
        var v = await db.VideoFiles.AsNoTracking()
            .Select(x => new { x.Id, x.FilePath })
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (v is null)
        {
            logger.LogWarning("Video not found: {Id}", id);
            return Result<string>.Fail("Video not found");
        }

        var fanartPath = FindImageWithAnyExtension(v.FilePath, "-fanart");
        if (fanartPath is null)
            return Result<string>.Fail("Fanart not found");

        return Result<string>.Ok(fanartPath);
    }

    public async Task<Result<VideoFileDto>> UploadFanartAsync(
        int id, Stream imageStream, string contentType, long contentLength,
        CancellationToken ct = default)
    {
        var v = await db.VideoFiles
            .Include(x => x.Studio)
            .Include(x => x.VideoActors).ThenInclude(va => va.Actor)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (v is null)
        {
            logger.LogWarning("Video not found for fanart upload: {Id}", id);
            return Result<VideoFileDto>.Fail("Video not found");
        }

        if (contentLength > 0 && contentLength > MaxPosterBytes)
            return Result<VideoFileDto>.Fail("File too large (max 10 MB)");

        if (!AllowedMimeTypes.Contains(contentType.ToLowerInvariant()))
            return Result<VideoFileDto>.Fail("Invalid file type. Only JPEG, PNG, and WebP are allowed");

        var destPath = FileSystemScanner.FanartPath(v.FilePath);
        try
        {
            await using var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await imageStream.CopyToAsync(fs, ct);
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "Failed to save fanart for video {Id}", id);
            try { File.Delete(destPath); } catch { /* ignore cleanup errors */ }
            return Result<VideoFileDto>.Fail("Failed to save fanart file");
        }

        v.HasFanart = true;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Fanart uploaded for video {Id} ({FileName})", id, v.FileName);
        return Result<VideoFileDto>.Ok(ToDto(v));
    }

    public async Task<Result<VideoFileDto>> ImportPosterFromPathAsync(int id, string path, CancellationToken ct = default)
    {
        var validationError = await ValidateImportPath(id, path, ct);
        if (validationError is not null)
            return Result<VideoFileDto>.Fail(validationError);

        var ext = Path.GetExtension(path).ToLowerInvariant();
        var mime = ext switch { ".png" => "image/png", ".webp" => "image/webp", ".jpg" or ".jpeg" => "image/jpeg", _ => null };
        if (mime is null)
            return Result<VideoFileDto>.Fail("Invalid file type. Only JPEG, PNG, and WebP are allowed");

        var info = new FileInfo(path);
        await using var stream = File.OpenRead(path);
        return await UploadPosterAsync(id, stream, mime, info.Length, ct);
    }

    public async Task<Result<VideoFileDto>> ImportFanartFromPathAsync(int id, string path, CancellationToken ct = default)
    {
        var validationError = await ValidateImportPath(id, path, ct);
        if (validationError is not null)
            return Result<VideoFileDto>.Fail(validationError);

        var ext = Path.GetExtension(path).ToLowerInvariant();
        var mime = ext switch { ".png" => "image/png", ".webp" => "image/webp", ".jpg" or ".jpeg" => "image/jpeg", _ => null };
        if (mime is null)
            return Result<VideoFileDto>.Fail("Invalid file type. Only JPEG, PNG, and WebP are allowed");

        var info = new FileInfo(path);
        await using var stream = File.OpenRead(path);
        return await UploadFanartAsync(id, stream, mime, info.Length, ct);
    }

    /// <summary>
    /// Validates that <paramref name="path"/> exists, and is within the library directory
    /// of the video with the given <paramref name="videoId"/>.
    /// Returns null on success, or an error message string on failure.
    /// </summary>
    private async Task<string?> ValidateImportPath(int videoId, string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            return "File not found";

        var video = await db.VideoFiles.AsNoTracking()
            .Select(v => new { v.Id, v.LibraryId })
            .FirstOrDefaultAsync(v => v.Id == videoId, ct);
        if (video is null)
            return "Video not found";

        var library = await db.Libraries.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == video.LibraryId, ct);
        if (library is null)
            return "Library not found";

        if (!FileSystemScanner.IsPathWithinBoundary(library.Path, path))
            return "Import path must be within the library directory";

        return null;
    }

    public async Task<Result<BatchUpdateResult>> BatchUpdateAsync(BatchUpdateVideoRequest request, CancellationToken ct = default)
    {
        if (request.Ids.Length == 0)
            return Result<BatchUpdateResult>.Ok(new BatchUpdateResult(0, []));

        // Pre-resolve studio once if needed
        Studio? newStudio = null;
        bool changeStudio = request.StudioName is not null;
        if (changeStudio && !string.IsNullOrEmpty(request.StudioName))
        {
            newStudio = await db.Studios.FirstOrDefaultAsync(s => s.Name == request.StudioName, ct)
                ?? new Studio { Name = request.StudioName };
            if (newStudio.Id == 0) db.Studios.Add(newStudio);
            await db.SaveChangesAsync(ct);
        }

        var failed = new List<int>();
        int updated = 0;

        foreach (var id in request.Ids)
        {
            try
            {
                var v = await db.VideoFiles
                    .Include(v => v.Studio)
                    .Include(v => v.VideoActors).ThenInclude(va => va.Actor)
                    .FirstOrDefaultAsync(v => v.Id == id, ct);

                if (v is null)
                {
                    logger.LogWarning("Batch update: video not found: {Id}", id);
                    failed.Add(id);
                    continue;
                }

                if (request.Title is not null) v.Title = request.Title;
                if (request.OriginalTitle is not null) v.OriginalTitle = request.OriginalTitle;
                if (request.Year is not null) v.Year = request.Year;
                if (request.Plot is not null) v.Plot = request.Plot;
                if (changeStudio)
                {
                    v.Studio = newStudio;
                    v.StudioId = newStudio?.Id;
                }
                if (request.Directors is not null) v.DirectorsJson = SerializeList(request.Directors);
                if (request.Genres is not null) v.GenresJson = SerializeList(request.Genres);
                if (request.Runtime is not null) v.Runtime = request.Runtime;
                if (request.Mpaa is not null) v.Mpaa = request.Mpaa;
                if (request.Premiered is not null) v.Premiered = request.Premiered;
                if (request.UserRating is not null) v.UserRating = request.UserRating;
                if (request.Tags is not null) v.TagsJson = SerializeList(request.Tags);
                if (request.SortTitle is not null) v.SortTitle = request.SortTitle;
                if (request.Outline is not null) v.Outline = request.Outline;
                if (request.Tagline is not null) v.Tagline = request.Tagline;
                if (request.Credits is not null) v.CreditsJson = SerializeList(request.Credits);
                if (request.Countries is not null) v.CountriesJson = SerializeList(request.Countries);
                if (request.SetName is not null) v.SetName = request.SetName;
                if (request.DateAdded is not null) v.DateAdded = request.DateAdded;
                if (request.Top250 is not null) v.Top250 = request.Top250;
                v.NfoUpdatedAt = DateTime.UtcNow;

                await db.SaveChangesAsync(ct);

                var dto = ToDto(v);
                await nfoService.WriteAsync(FileSystemScanner.NfoPath(v.FilePath), dto, ct);

                if (!v.HasNfo)
                {
                    v.HasNfo = true;
                    await db.SaveChangesAsync(ct);
                }

                updated++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Batch update failed for video {Id}", id);
                failed.Add(id);
            }
        }

        logger.LogInformation("Batch update complete: {Updated} updated, {FailedCount} failed", updated, failed.Count);
        return Result<BatchUpdateResult>.Ok(new BatchUpdateResult(updated, [.. failed]));
    }

    public async Task<Result<BatchDeleteResult>> BatchDeleteAsync(BatchDeleteRequest request, CancellationToken ct = default)
    {
        if (request.Ids.Length == 0)
            return Result<BatchDeleteResult>.Ok(new BatchDeleteResult(0, []));

        var failed = new List<int>();
        int deleted = 0;

        foreach (var id in request.Ids)
        {
            try
            {
                var v = await db.VideoFiles
                    .Include(v => v.VideoActors)
                    .FirstOrDefaultAsync(v => v.Id == id, ct);

                if (v is null)
                {
                    logger.LogWarning("Batch delete: video not found: {Id}", id);
                    failed.Add(id);
                    continue;
                }

                // Delete metadata files (NFO, poster, fanart)
                if (request.Mode is DeleteMode.Metadata or DeleteMode.All)
                {
                    DeleteFileIfExists(FileSystemScanner.NfoPath(v.FilePath));
                    var posterPath = FindImageWithAnyExtension(v.FilePath, "-poster");
                    if (posterPath is not null) DeleteFileIfExists(posterPath);
                    var fanartPath = FindImageWithAnyExtension(v.FilePath, "-fanart");
                    if (fanartPath is not null) DeleteFileIfExists(fanartPath);

                    v.HasNfo = false;
                    v.HasPoster = false;
                    v.HasFanart = false;
                    v.Title = null;
                    v.OriginalTitle = null;
                    v.Year = null;
                    v.Plot = null;
                    v.StudioId = null;
                    v.NfoUpdatedAt = null;
                    v.DirectorsJson = null;
                    v.GenresJson = null;
                    v.Runtime = null;
                    v.Mpaa = null;
                    v.Premiered = null;
                    v.RatingsJson = null;
                    v.UserRating = null;
                    v.UniqueIdsJson = null;
                    v.TagsJson = null;
                    v.SortTitle = null;
                    v.Outline = null;
                    v.Tagline = null;
                    v.CreditsJson = null;
                    v.CountriesJson = null;
                    v.SetName = null;
                    v.DateAdded = null;
                    v.Top250 = null;
                }

                // Delete video file + remove DB record
                if (request.Mode is DeleteMode.Video or DeleteMode.All)
                {
                    DeleteFileIfExists(v.FilePath);
                    db.VideoActors.RemoveRange(v.VideoActors);
                    db.VideoFiles.Remove(v);
                }

                await db.SaveChangesAsync(ct);
                deleted++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Batch delete failed for video {Id}", id);
                failed.Add(id);
            }
        }

        logger.LogInformation("Batch delete complete (mode={Mode}): {Deleted} deleted, {FailedCount} failed",
            request.Mode, deleted, failed.Count);
        return Result<BatchDeleteResult>.Ok(new BatchDeleteResult(deleted, [.. failed]));
    }

    public async Task<Result> RevealInFileManagerAsync(int id, CancellationToken ct = default)
    {
        var filePath = await db.VideoFiles
            .Where(v => v.Id == id)
            .Select(v => v.FilePath)
            .FirstOrDefaultAsync(ct);

        if (filePath is null)
            return Result.Fail("Video not found");

        if (!File.Exists(filePath))
            return Result.Fail("File not found on disk");

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", $"-R \"{filePath}\"");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
            else
            {
                var dir = Path.GetDirectoryName(filePath)!;
                Process.Start("xdg-open", $"\"{dir}\"");
            }

            logger.LogInformation("Revealed file in file manager: {FilePath}", filePath);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to reveal file in file manager: {FilePath}", filePath);
            return Result.Fail("Cannot open file manager in this environment");
        }
    }

    public async Task<Result> OpenVideoFileAsync(int id, CancellationToken ct = default)
    {
        var filePath = await db.VideoFiles
            .Where(v => v.Id == id)
            .Select(v => v.FilePath)
            .FirstOrDefaultAsync(ct);

        if (filePath is null)
            return Result.Fail("Video not found");

        if (!File.Exists(filePath))
            return Result.Fail("File not found on disk");

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", $"\"{filePath}\"");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            else
            {
                Process.Start("xdg-open", $"\"{filePath}\"");
            }

            logger.LogInformation("Opened video file: {FilePath}", filePath);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to open video file: {FilePath}", filePath);
            return Result.Fail("Cannot open video file in this environment");
        }
    }

    private static void DeleteFileIfExists(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort cleanup */ }
    }

    private static string? FindImageWithAnyExtension(string videoFilePath, string suffix)
    {
        var dir = Path.GetDirectoryName(videoFilePath)!;
        var stem = Path.GetFileNameWithoutExtension(videoFilePath);
        foreach (var ext in new[] { ".jpg", ".jpeg", ".png" })
        {
            var path = Path.Combine(dir, stem + suffix + ext);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    private static VideoFileDto ToDto(VideoFile v)
    {
        var actors = v.VideoActors
            .OrderBy(va => va.Order)
            .Select(va => new ActorDto(va.Actor.Id, va.Actor.Name, va.Role, va.Order))
            .ToList();

        return new VideoFileDto(
            v.Id, v.LibraryId, v.FileName, v.FilePath, v.FileSizeBytes,
            v.HasNfo, v.HasPoster, v.HasFanart, v.Title, v.OriginalTitle, v.Year, v.Plot,
            v.Studio?.Name, v.ScannedAt, actors, v.FileModifiedAt,
            Directors: DeserializeList(v.DirectorsJson),
            Genres: DeserializeList(v.GenresJson),
            Runtime: v.Runtime,
            Mpaa: v.Mpaa,
            Premiered: v.Premiered,
            Ratings: DeserializeJson<List<RatingDto>>(v.RatingsJson),
            UserRating: v.UserRating,
            UniqueIds: DeserializeJson<Dictionary<string, string>>(v.UniqueIdsJson),
            Tags: DeserializeList(v.TagsJson),
            SortTitle: v.SortTitle,
            Outline: v.Outline,
            Tagline: v.Tagline,
            Credits: DeserializeList(v.CreditsJson),
            Countries: DeserializeList(v.CountriesJson),
            SetName: v.SetName,
            DateAdded: v.DateAdded,
            Top250: v.Top250
        );
    }

    private static string? SerializeList(IReadOnlyList<string>? list)
        => list is { Count: > 0 } ? JsonSerializer.Serialize(list) : null;

    private static List<string>? DeserializeList(string? json)
        => string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<List<string>>(json);

    private static T? DeserializeJson<T>(string? json) where T : class
        => string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<T>(json);
}
