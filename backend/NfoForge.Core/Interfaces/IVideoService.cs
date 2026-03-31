using NfoForge.Core.Models;

namespace NfoForge.Core.Interfaces;

public record ActorDto(int Id, string Name, string? Role, int Order);

public record VideoFileDto(
    int Id,
    int LibraryId,
    string FileName,
    string FilePath,
    long FileSizeBytes,
    bool HasNfo,
    bool HasPoster,
    bool HasFanart,
    string? Title,
    string? OriginalTitle,
    int? Year,
    string? Plot,
    string? StudioName,
    DateTime ScannedAt,
    IReadOnlyList<ActorDto>? Actors = null,
    DateTime? FileModifiedAt = null
);

public record VideoFileFilter(
    bool? HasNfo = null,
    bool? HasPoster = null,
    int? LibraryId = null,
    int? StudioId = null,
    string? Search = null,
    string? SortBy = null,
    bool SortDesc = false
);

public record ActorRequest(string Name, string? Role, int Order);

public record UpdateVideoRequest(
    string? Title,
    string? OriginalTitle,
    int? Year,
    string? Plot,
    string? StudioName,
    IReadOnlyList<ActorRequest>? Actors = null
);

public record ImportFromPathRequest(string Path);

public interface IVideoService
{
    Task<Result<PagedResult<VideoFileDto>>> GetAllAsync(
        VideoFileFilter filter, int page, int pageSize, CancellationToken ct = default);
    Task<Result<VideoFileDto>> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<VideoFileDto>> UpdateAsync(int id, UpdateVideoRequest request, CancellationToken ct = default);
    Task<Result<string>> GetPosterPathAsync(int id, CancellationToken ct = default);
    Task<Result<VideoFileDto>> UploadPosterAsync(
        int id, Stream imageStream, string contentType, long contentLength,
        CancellationToken ct = default);
    Task<Result<string>> GetFanartPathAsync(int id, CancellationToken ct = default);
    Task<Result<VideoFileDto>> UploadFanartAsync(
        int id, Stream imageStream, string contentType, long contentLength,
        CancellationToken ct = default);
    Task<Result<VideoFileDto>> ImportPosterFromPathAsync(int id, string path, CancellationToken ct = default);
    Task<Result<VideoFileDto>> ImportFanartFromPathAsync(int id, string path, CancellationToken ct = default);
}
