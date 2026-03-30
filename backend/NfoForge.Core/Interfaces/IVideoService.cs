using NfoForge.Core.Models;

namespace NfoForge.Core.Interfaces;

public record VideoFileDto(
    int Id,
    int LibraryId,
    string FileName,
    string FilePath,
    long FileSizeBytes,
    bool HasNfo,
    bool HasPoster,
    string? Title,
    string? OriginalTitle,
    int? Year,
    string? Plot,
    string? StudioName,
    DateTime ScannedAt
);

public record VideoFileFilter(
    bool? HasNfo = null,
    bool? HasPoster = null,
    int? LibraryId = null,
    int? StudioId = null,
    string? Search = null
);

public interface IVideoService
{
    Task<Result<PagedResult<VideoFileDto>>> GetAllAsync(
        VideoFileFilter filter, int page, int pageSize, CancellationToken ct = default);
    Task<Result<VideoFileDto>> GetByIdAsync(int id, CancellationToken ct = default);
}
