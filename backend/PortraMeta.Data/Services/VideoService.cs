using System.Diagnostics;
using System.Linq.Expressions;
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

    // File magic byte signatures for image validation
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47];
    private static readonly byte[] WebpRiff = [0x52, 0x49, 0x46, 0x46]; // "RIFF"
    private static readonly byte[] WebpTag = [0x57, 0x45, 0x42, 0x50]; // "WEBP"

    public async Task<Result<PagedResult<VideoFileDto>>> GetAllAsync(
        VideoFileFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.VideoFiles.AsQueryable();

        if (filter.HasNfo.HasValue)
            query = query.Where(v => v.HasNfo == filter.HasNfo.Value);
        if (filter.HasPoster.HasValue)
            query = query.Where(v => v.HasPoster == filter.HasPoster.Value);
        if (filter.HasFanart.HasValue)
            query = query.Where(v => v.HasFanart == filter.HasFanart.Value);
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

        if (filter.AdvancedFilters is { Count: > 0 })
            query = ApplyAdvancedFilters(query, filter.AdvancedFilters, filter.FilterLogic);

        var total = await query.CountAsync(ct);

        // Helper: for nullable columns, always push NULL values to the end regardless of sort direction.
        // Ascending:  non-null first (A→Z), then nulls.
        // Descending: non-null first (Z→A), then nulls.
        IQueryable<VideoFile> sorted = (filter.SortBy, filter.SortDesc) switch
        {
            ("title", false)          => query.OrderBy(v => v.Title ?? v.FileName),
            ("title", true)           => query.OrderByDescending(v => v.Title ?? v.FileName),
            ("year", false)           => query.OrderBy(v => v.Year == null ? 1 : 0).ThenBy(v => v.Year),
            ("year", true)            => query.OrderBy(v => v.Year == null ? 1 : 0).ThenByDescending(v => v.Year),
            ("studioName", false)     => query.OrderBy(v => v.Studio == null ? 1 : 0).ThenBy(v => v.Studio!.Name),
            ("studioName", true)      => query.OrderBy(v => v.Studio == null ? 1 : 0).ThenByDescending(v => v.Studio!.Name),
            ("fileSizeBytes", false)  => query.OrderBy(v => v.FileSizeBytes),
            ("fileSizeBytes", true)   => query.OrderByDescending(v => v.FileSizeBytes),
            ("scannedAt", false)      => query.OrderBy(v => v.ScannedAt),
            ("scannedAt", true)       => query.OrderByDescending(v => v.ScannedAt),
            ("fileModifiedAt", false) => query.OrderBy(v => v.FileModifiedAt == null ? 1 : 0).ThenBy(v => v.FileModifiedAt),
            ("fileModifiedAt", true)  => query.OrderBy(v => v.FileModifiedAt == null ? 1 : 0).ThenByDescending(v => v.FileModifiedAt),
            ("originalTitle", false)  => query.OrderBy(v => v.OriginalTitle == null ? 1 : 0).ThenBy(v => v.OriginalTitle),
            ("originalTitle", true)   => query.OrderBy(v => v.OriginalTitle == null ? 1 : 0).ThenByDescending(v => v.OriginalTitle),
            ("sortTitle", false)      => query.OrderBy(v => v.SortTitle == null ? 1 : 0).ThenBy(v => v.SortTitle),
            ("sortTitle", true)       => query.OrderBy(v => v.SortTitle == null ? 1 : 0).ThenByDescending(v => v.SortTitle),
            ("runtime", false)        => query.OrderBy(v => v.Runtime == null ? 1 : 0).ThenBy(v => v.Runtime),
            ("runtime", true)         => query.OrderBy(v => v.Runtime == null ? 1 : 0).ThenByDescending(v => v.Runtime),
            ("mpaa", false)           => query.OrderBy(v => v.Mpaa == null ? 1 : 0).ThenBy(v => v.Mpaa),
            ("mpaa", true)            => query.OrderBy(v => v.Mpaa == null ? 1 : 0).ThenByDescending(v => v.Mpaa),
            ("premiered", false)      => query.OrderBy(v => v.Premiered == null ? 1 : 0).ThenBy(v => v.Premiered),
            ("premiered", true)       => query.OrderBy(v => v.Premiered == null ? 1 : 0).ThenByDescending(v => v.Premiered),
            ("userRating", false)     => query.OrderBy(v => v.UserRating == null ? 1 : 0).ThenBy(v => v.UserRating),
            ("userRating", true)      => query.OrderBy(v => v.UserRating == null ? 1 : 0).ThenByDescending(v => v.UserRating),
            ("top250", false)         => query.OrderBy(v => v.Top250 == null ? 1 : 0).ThenBy(v => v.Top250),
            ("top250", true)          => query.OrderBy(v => v.Top250 == null ? 1 : 0).ThenByDescending(v => v.Top250),
            ("outline", false)        => query.OrderBy(v => v.Outline == null ? 1 : 0).ThenBy(v => v.Outline),
            ("outline", true)         => query.OrderBy(v => v.Outline == null ? 1 : 0).ThenByDescending(v => v.Outline),
            ("tagline", false)        => query.OrderBy(v => v.Tagline == null ? 1 : 0).ThenBy(v => v.Tagline),
            ("tagline", true)         => query.OrderBy(v => v.Tagline == null ? 1 : 0).ThenByDescending(v => v.Tagline),
            ("setName", false)        => query.OrderBy(v => v.SetName == null ? 1 : 0).ThenBy(v => v.SetName),
            ("setName", true)         => query.OrderBy(v => v.SetName == null ? 1 : 0).ThenByDescending(v => v.SetName),
            ("dateAdded", false)      => query.OrderBy(v => v.DateAdded == null ? 1 : 0).ThenBy(v => v.DateAdded),
            ("dateAdded", true)       => query.OrderBy(v => v.DateAdded == null ? 1 : 0).ThenByDescending(v => v.DateAdded),
            ("fileName", true)        => query.OrderByDescending(v => v.FileName),
            _                         => query.OrderBy(v => v.FileName),
        };

        var rawItems = await sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new {
                v.Id, v.LibraryId, v.FileName, v.FilePath, v.FileSizeBytes,
                v.HasNfo, v.HasPoster, v.HasFanart, v.Title, v.OriginalTitle, v.Year, v.Plot,
                StudioName = v.Studio != null ? v.Studio.Name : null,
                v.ScannedAt, v.FileModifiedAt,
                v.DirectorsJson, v.GenresJson, v.Runtime, v.Mpaa, v.Premiered,
                v.RatingsJson, v.UserRating, v.UniqueIdsJson, v.TagsJson, v.SortTitle,
                v.Outline, v.Tagline, v.CreditsJson, v.CountriesJson,
                v.SetName, v.DateAdded, v.Top250
            })
            .ToListAsync(ct);

        var items = rawItems.Select(v => new VideoFileDto(
            v.Id, v.LibraryId, v.FileName, v.FilePath, v.FileSizeBytes,
            v.HasNfo, v.HasPoster, v.HasFanart, v.Title, v.OriginalTitle, v.Year, v.Plot,
            v.StudioName, v.ScannedAt,
            null, v.FileModifiedAt,
            Directors: DeserializeList(v.DirectorsJson),
            Genres: DeserializeList(v.GenresJson),
            Runtime: v.Runtime, Mpaa: v.Mpaa, Premiered: v.Premiered,
            Ratings: DeserializeJson<List<RatingDto>>(v.RatingsJson),
            UserRating: v.UserRating,
            UniqueIds: DeserializeJson<Dictionary<string, string>>(v.UniqueIdsJson),
            Tags: DeserializeList(v.TagsJson),
            SortTitle: v.SortTitle,
            Outline: v.Outline, Tagline: v.Tagline,
            Credits: DeserializeList(v.CreditsJson),
            Countries: DeserializeList(v.CountriesJson),
            SetName: v.SetName, DateAdded: v.DateAdded, Top250: v.Top250
        )).ToList();

        logger.LogDebug("GetAll videos: page={Page}, pageSize={PageSize}, total={Total}", page, pageSize, total);
        return Result<PagedResult<VideoFileDto>>.Ok(new PagedResult<VideoFileDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    private static IQueryable<VideoFile> ApplyAdvancedFilters(
        IQueryable<VideoFile> query, IReadOnlyList<AdvancedFilterItem> items, string logic)
    {
        var predicates = new List<Expression<Func<VideoFile, bool>>>();

        foreach (var item in items)
        {
            var predicate = BuildPredicate(item);
            if (predicate != null)
                predicates.Add(predicate);
        }

        if (predicates.Count == 0)
            return query;

        var combined = predicates[0];
        for (var i = 1; i < predicates.Count; i++)
        {
            combined = logic.Equals("or", StringComparison.OrdinalIgnoreCase)
                ? CombineOr(combined, predicates[i])
                : CombineAnd(combined, predicates[i]);
        }

        return query.Where(combined);
    }

    private static Expression<Func<VideoFile, bool>>? BuildPredicate(AdvancedFilterItem item)
    {
        var op = item.Op.ToLowerInvariant();
        var val = item.Value;

        return item.Field.ToLowerInvariant() switch
        {
            // Boolean fields
            "hasnfo" => BoolPredicate(v => v.HasNfo, val),
            "hasposter" => BoolPredicate(v => v.HasPoster, val),
            "hasfanart" => BoolPredicate(v => v.HasFanart, val),
            // Text fields (contains / equals)
            "title" => TextPredicate(v => v.Title, op, val),
            "originaltitle" => TextPredicate(v => v.OriginalTitle, op, val),
            "sorttitle" => TextPredicate(v => v.SortTitle, op, val),
            "filename" => TextPredicate(v => v.FileName, op, val),
            "studioname" => TextPredicate(v => v.Studio != null ? v.Studio.Name : null, op, val),
            "plot" => TextPredicate(v => v.Plot, op, val),
            "outline" => TextPredicate(v => v.Outline, op, val),
            "tagline" => TextPredicate(v => v.Tagline, op, val),
            "mpaa" => TextPredicate(v => v.Mpaa, op, val),
            "premiered" => TextPredicate(v => v.Premiered, op, val),
            "setname" => TextPredicate(v => v.SetName, op, val),
            "dateadded" => TextPredicate(v => v.DateAdded, op, val),
            // JSON array fields (contains search within JSON string)
            "directors" => TextPredicate(v => v.DirectorsJson, op, val),
            "genres" => TextPredicate(v => v.GenresJson, op, val),
            "tags" => TextPredicate(v => v.TagsJson, op, val),
            "credits" => TextPredicate(v => v.CreditsJson, op, val),
            "countries" => TextPredicate(v => v.CountriesJson, op, val),
            // Numeric fields
            "year" => NumericPredicate(v => v.Year, op, val),
            "runtime" => NumericPredicate(v => v.Runtime, op, val),
            "userrating" => NumericPredicate(v => v.UserRating, op, val),
            "top250" => NumericPredicate(v => v.Top250, op, val),
            _ => null
        };
    }

    private static Expression<Func<VideoFile, bool>> BoolPredicate(
        Expression<Func<VideoFile, bool>> selector, string val)
    {
        var isTrue = val.Equals("true", StringComparison.OrdinalIgnoreCase);
        var param = selector.Parameters[0];
        var body = isTrue
            ? selector.Body
            : Expression.Not(selector.Body);
        return Expression.Lambda<Func<VideoFile, bool>>(body, param);
    }

    private static Expression<Func<VideoFile, bool>>? TextPredicate(
        Expression<Func<VideoFile, string?>> selector, string op, string val)
    {
        var param = selector.Parameters[0];
        var member = selector.Body;
        var pattern = Expression.Constant($"%{val}%");
        var valConst = Expression.Constant(val);

        Expression body = op switch
        {
            "contains" => MakeLikeCall(member, pattern),
            "equals" => Expression.Equal(member, valConst),
            "notequals" => Expression.NotEqual(member, valConst),
            "startswith" => MakeLikeCall(member, Expression.Constant($"{val}%")),
            "endswith" => MakeLikeCall(member, Expression.Constant($"%{val}")),
            "isempty" => Expression.OrElse(
                Expression.Equal(member, Expression.Constant(null, typeof(string))),
                Expression.Equal(member, Expression.Constant(""))),
            "isnotempty" => Expression.AndAlso(
                Expression.NotEqual(member, Expression.Constant(null, typeof(string))),
                Expression.NotEqual(member, Expression.Constant(""))),
            _ => MakeLikeCall(member, pattern)
        };

        return Expression.Lambda<Func<VideoFile, bool>>(body, param);
    }

    private static MethodCallExpression MakeLikeCall(Expression member, Expression pattern)
    {
        var likeMethod = typeof(DbFunctionsExtensions).GetMethod(
            nameof(DbFunctionsExtensions.Like),
            [typeof(DbFunctions), typeof(string), typeof(string)])!;
        return Expression.Call(likeMethod, Expression.Constant(EF.Functions), member, pattern);
    }

    private static Expression<Func<VideoFile, bool>>? NumericPredicate(
        Expression<Func<VideoFile, int?>> selector, string op, string val)
    {
        if (!int.TryParse(val, out var num))
            return null;

        var param = selector.Parameters[0];
        var member = selector.Body;
        var valExpr = Expression.Convert(Expression.Constant(num), typeof(int?));

        Expression body = op switch
        {
            "eq" or "equals" => Expression.Equal(member, valExpr),
            "neq" or "notequals" => Expression.NotEqual(member, valExpr),
            "gt" => Expression.GreaterThan(member, valExpr),
            "gte" => Expression.GreaterThanOrEqual(member, valExpr),
            "lt" => Expression.LessThan(member, valExpr),
            "lte" => Expression.LessThanOrEqual(member, valExpr),
            _ => Expression.Equal(member, valExpr)
        };

        return Expression.Lambda<Func<VideoFile, bool>>(body, param);
    }

    private static Expression<Func<VideoFile, bool>> CombineAnd(
        Expression<Func<VideoFile, bool>> left, Expression<Func<VideoFile, bool>> right)
    {
        var param = left.Parameters[0];
        var body = Expression.AndAlso(
            left.Body,
            new ParameterReplacer(right.Parameters[0], param).Visit(right.Body));
        return Expression.Lambda<Func<VideoFile, bool>>(body, param);
    }

    private static Expression<Func<VideoFile, bool>> CombineOr(
        Expression<Func<VideoFile, bool>> left, Expression<Func<VideoFile, bool>> right)
    {
        var param = left.Parameters[0];
        var body = Expression.OrElse(
            left.Body,
            new ParameterReplacer(right.Parameters[0], param).Visit(right.Body));
        return Expression.Lambda<Func<VideoFile, bool>>(body, param);
    }

    private sealed class ParameterReplacer(ParameterExpression oldParam, ParameterExpression newParam) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == oldParam ? newParam : base.VisitParameter(node);
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

        if (!ValidateImageMagicBytes(imageStream))
            return Result<VideoFileDto>.Fail("File content does not match a supported image format");

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

        if (!ValidateImageMagicBytes(imageStream))
            return Result<VideoFileDto>.Fail("File content does not match a supported image format");

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

        // Load all target videos in a single query to avoid N+1
        var videos = await db.VideoFiles
            .Where(v => request.Ids.Contains(v.Id))
            .Include(v => v.Studio)
            .Include(v => v.VideoActors).ThenInclude(va => va.Actor)
            .ToDictionaryAsync(v => v.Id, ct);

        var failed = new List<int>();
        int updated = 0;

        foreach (var id in request.Ids)
        {
            try
            {
                if (!videos.TryGetValue(id, out var v))
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
                var psi = new ProcessStartInfo("open") { UseShellExecute = false };
                psi.ArgumentList.Add("-R");
                psi.ArgumentList.Add(filePath);
                Process.Start(psi);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var psi = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
                psi.ArgumentList.Add($"/select,{filePath}");
                Process.Start(psi);
            }
            else
            {
                var dir = Path.GetDirectoryName(filePath)!;
                var psi = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
                psi.ArgumentList.Add(dir);
                Process.Start(psi);
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
                var psi = new ProcessStartInfo("open") { UseShellExecute = false };
                psi.ArgumentList.Add(filePath);
                Process.Start(psi);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Use cmd /c start to open with default association without UseShellExecute
                var psi = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true };
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add("start");
                psi.ArgumentList.Add("");
                psi.ArgumentList.Add(filePath);
                Process.Start(psi);
            }
            else
            {
                var psi = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
                psi.ArgumentList.Add(filePath);
                Process.Start(psi);
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

    /// <summary>
    /// Validates that the stream content starts with a known image file signature.
    /// The stream position is reset to the beginning after validation.
    /// </summary>
    private static bool ValidateImageMagicBytes(Stream stream)
    {
        if (!stream.CanSeek) return true; // skip validation if stream is not seekable

        var header = new byte[12];
        var bytesRead = stream.Read(header, 0, header.Length);
        stream.Position = 0;

        if (bytesRead < 3) return false;

        // JPEG: FF D8 FF
        if (header.AsSpan(0, 3).SequenceEqual(JpegMagic))
            return true;

        // PNG: 89 50 4E 47
        if (bytesRead >= 4 && header.AsSpan(0, 4).SequenceEqual(PngMagic))
            return true;

        // WebP: RIFF....WEBP
        if (bytesRead >= 12 && header.AsSpan(0, 4).SequenceEqual(WebpRiff)
                            && header.AsSpan(8, 4).SequenceEqual(WebpTag))
            return true;

        return false;
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
