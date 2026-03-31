using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NfoForge.Core.Interfaces;
using NfoForge.Core.Models;
using NfoForge.Data.Entities;
using NfoForge.Data.Utilities;

namespace NfoForge.Data.Services;

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
            query = query.Where(v => v.Title!.Contains(filter.Search) || v.FileName.Contains(filter.Search));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(v => v.FileName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new VideoFileDto(
                v.Id, v.LibraryId, v.FileName, v.FilePath, v.FileSizeBytes,
                v.HasNfo, v.HasPoster, v.HasFanart, v.Title, v.OriginalTitle, v.Year, v.Plot,
                v.Studio != null ? v.Studio.Name : null, v.ScannedAt, null))
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
        if (!File.Exists(path))
            return Result<VideoFileDto>.Fail("File not found");

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
        if (!File.Exists(path))
            return Result<VideoFileDto>.Fail("File not found");

        var ext = Path.GetExtension(path).ToLowerInvariant();
        var mime = ext switch { ".png" => "image/png", ".webp" => "image/webp", ".jpg" or ".jpeg" => "image/jpeg", _ => null };
        if (mime is null)
            return Result<VideoFileDto>.Fail("Invalid file type. Only JPEG, PNG, and WebP are allowed");

        var info = new FileInfo(path);
        await using var stream = File.OpenRead(path);
        return await UploadFanartAsync(id, stream, mime, info.Length, ct);
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
            v.Studio?.Name, v.ScannedAt, actors);
    }
}
