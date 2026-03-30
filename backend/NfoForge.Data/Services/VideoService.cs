using Microsoft.EntityFrameworkCore;
using NfoForge.Core.Interfaces;
using NfoForge.Core.Models;

namespace NfoForge.Data.Services;

public class VideoService(AppDbContext db) : IVideoService
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
                v.HasNfo, v.HasPoster, v.Title, v.OriginalTitle, v.Year, v.Plot,
                v.Studio != null ? v.Studio.Name : null, v.ScannedAt))
            .ToListAsync(ct);

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
            .FirstOrDefaultAsync(v => v.Id == id, ct);

        if (v is null) return Result<VideoFileDto>.Fail("Video not found");

        return Result<VideoFileDto>.Ok(new VideoFileDto(
            v.Id, v.LibraryId, v.FileName, v.FilePath, v.FileSizeBytes,
            v.HasNfo, v.HasPoster, v.Title, v.OriginalTitle, v.Year, v.Plot,
            v.Studio?.Name, v.ScannedAt));
    }
}
