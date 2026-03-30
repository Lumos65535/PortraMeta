using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NfoForge.Core.Interfaces;
using NfoForge.Core.Models;
using NfoForge.Data.Entities;
using NfoForge.Data.Utilities;

namespace NfoForge.Data.Services;

public class VideoService(AppDbContext db, INfoService nfoService, ILogger<VideoService> logger) : IVideoService
{
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

        v.NfoUpdatedAt = DateTime.UtcNow;
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
